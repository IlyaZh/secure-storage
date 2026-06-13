using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using Xunit;

namespace SecureStorage.Tests;

public class SecretServiceTests : IDisposable
{
    private class MockOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value => value;
        public T Get(string? name) => value;
    }

    private IOptionsSnapshot<AppSettings> CreateMockSettings(long quotaBytes = 209715200)
    {
        return new MockOptionsSnapshot<AppSettings>(new AppSettings
        {
            QuotaBytes = quotaBytes
        });
    }
    private readonly string _storagePath;

    public SecretServiceTests()
    {
        // Setup isolated Storage folder in test build directory
        _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, true);
        }
    }

    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateSecretAsync_ShouldSucceed_WhenUserExists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        var content = "Secret Content 123";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var iv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        // Act
        var secretId = await service.CreateSecretAsync(
            stream,
            ownerId,
            "My Comment",
            isOneTime: true,
            iv,
            contentType: "text/plain",
            fileName: "secret.txt",
            remainingQuotaBytes: 209715200, // 200MB
            CancellationToken.None
        );

        // Assert
        Assert.NotEqual(Guid.Empty, secretId);

        var secret = await context.Secrets.FindAsync(secretId);
        Assert.NotNull(secret);
        Assert.Equal("My Comment", secret.Comment);
        Assert.True(secret.IsOneTime);
        Assert.Equal("text/plain", secret.ContentType);
        Assert.Equal("secret.txt", secret.FileName);
        Assert.Equal(content.Length, secret.Size);

        var quota = await context.UserQuota.FirstOrDefaultAsync(q => q.UserId == ownerId);
        Assert.NotNull(quota);
        Assert.Equal(content.Length, quota.UsedQuota);

        var savedFilePath = Path.Combine(_storagePath, secretId.ToString());
        Assert.True(File.Exists(savedFilePath));
        var savedContent = await File.ReadAllTextAsync(savedFilePath);
        Assert.Equal(content, savedContent);
    }

    [Fact]
    public async Task CreateSecretAsync_ShouldThrowException_WhenUserDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Data"));
        var iv = new byte[12];

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSecretAsync(
                stream,
                Guid.NewGuid(),
                "Comment",
                isOneTime: false,
                iv,
                contentType: "text/plain",
                fileName: null,
                remainingQuotaBytes: 209715200, // 200MB
                CancellationToken.None
            ));
    }

    [Fact]
    public async Task CreateSecretAsync_ShouldThrowException_WhenQuotaExceeded()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        var content = "Secret Content 123";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var iv = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        // Act & Assert
        // We set remaining quota to 5 bytes, but our content is 18 bytes.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSecretAsync(
                stream,
                ownerId,
                "Comment",
                isOneTime: false,
                iv,
                contentType: "text/plain",
                fileName: "test.txt",
                remainingQuotaBytes: 5,
                CancellationToken.None
            ));

        Assert.Equal("Storage quota exceeded.", exception.Message);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldReturnSecretAndBurnIt_WhenIsOneTime()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var secretId = Guid.NewGuid();
        var secret = new Secret
        {
            Id = secretId,
            OwnerId = ownerId,
            Comment = "One Time",
            IsOneTime = true,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[] { 1, 2, 3 },
            Size = 4,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.Secrets.Add(secret);
        await context.SaveChangesAsync();

        // Write raw file
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, secretId.ToString()), "Test");

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act
        var result = await service.GetSecretAsync(secretId, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test.txt", result.FileName);
        Assert.True(result.IsOneTime);
        Assert.Equal("Test", Encoding.UTF8.GetString(result.EncryptedData));

        // Check if DB record has been updated to Burned
        var updatedSecret = await context.Secrets.FindAsync(secretId);
        Assert.NotNull(updatedSecret);
        Assert.True(updatedSecret.IsBurned);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldReturnNull_WhenSecretIsExpiredOrAlreadyBurned()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var expiredSecretId = Guid.NewGuid();
        var expiredSecret = new Secret
        {
            Id = expiredSecretId,
            OwnerId = ownerId,
            Comment = "Expired",
            IsOneTime = false,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[0],
            Size = 0,
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            ExpiresAt = DateTime.UtcNow.AddHours(-5) // Expired
        };

        var burnedSecretId = Guid.NewGuid();
        var burnedSecret = new Secret
        {
            Id = burnedSecretId,
            OwnerId = ownerId,
            Comment = "Burned",
            IsOneTime = false,
            IsBurned = true, // Burned
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[0],
            Size = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        context.Secrets.Add(expiredSecret);
        context.Secrets.Add(burnedSecret);
        await context.SaveChangesAsync();

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act & Assert
        Assert.Null(await service.GetSecretAsync(expiredSecretId, null, CancellationToken.None));
        Assert.Null(await service.GetSecretAsync(burnedSecretId, null, CancellationToken.None));
    }

    [Fact]
    public async Task BurnSecretAsync_ShouldMarkAsBurned_WhenExists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var secretId = Guid.NewGuid();
        var secret = new Secret
        {
            Id = secretId,
            OwnerId = ownerId,
            Comment = "Normal",
            IsOneTime = false,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[0],
            Size = 100,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.Secrets.Add(secret);

        context.UserQuota.Add(new UserQuota
        {
            UserId = ownerId,
            Quota = 200L * 1024 * 1024,
            UsedQuota = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act
        await service.BurnSecretAsync(secretId, ownerId, CancellationToken.None);

        // Assert
        var updatedSecret = await context.Secrets.FindAsync(secretId);
        Assert.NotNull(updatedSecret);
        Assert.True(updatedSecret.IsBurned);
        Assert.True(updatedSecret.ExpiresAt <= DateTime.UtcNow);

        var quota = await context.UserQuota.FirstOrDefaultAsync(q => q.UserId == ownerId);
        Assert.NotNull(quota);
        Assert.Equal(0, quota.UsedQuota);
    }

    [Fact]
    public async Task CleanupExpiredSecretsBatchAsync_ShouldDeleteExpiredFilesAndDBRecords()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var expiredSecretId1 = Guid.NewGuid();
        var expiredSecret1 = new Secret
        {
            Id = expiredSecretId1,
            OwnerId = ownerId,
            Comment = "Expired 1",
            IsOneTime = false,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[0],
            Size = 10,
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired
        };

        var activeSecretId = Guid.NewGuid();
        var activeSecret = new Secret
        {
            Id = activeSecretId,
            OwnerId = ownerId,
            Comment = "Active",
            IsOneTime = false,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[0],
            Size = 20,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1) // Active
        };

        context.Secrets.Add(expiredSecret1);
        context.Secrets.Add(activeSecret);

        context.UserQuota.Add(new UserQuota
        {
            UserId = ownerId,
            Quota = 200L * 1024 * 1024,
            UsedQuota = 30,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Write files to disk
        Directory.CreateDirectory(_storagePath);
        var expiredFile = Path.Combine(_storagePath, expiredSecretId1.ToString());
        var activeFile = Path.Combine(_storagePath, activeSecretId.ToString());
        await File.WriteAllTextAsync(expiredFile, "Expired content");
        await File.WriteAllTextAsync(activeFile, "Active content");

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act
        var deletedCount = await service.CleanupExpiredSecretsBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(expiredFile));
        Assert.True(File.Exists(activeFile));

        Assert.Null(await context.Secrets.FindAsync(expiredSecretId1));
        Assert.NotNull(await context.Secrets.FindAsync(activeSecretId));

        var quota = await context.UserQuota.FirstOrDefaultAsync(q => q.UserId == ownerId);
        Assert.NotNull(quota);
        Assert.Equal(20, quota.UsedQuota);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldNotBurnOneTimeSecret_WhenAccessedByOwner()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var secretId = Guid.NewGuid();
        var secret = new Secret
        {
            Id = secretId,
            OwnerId = ownerId,
            Comment = "One Time",
            IsOneTime = true,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[] { 1, 2, 3 },
            Size = 4,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.Secrets.Add(secret);
        await context.SaveChangesAsync();

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, secretId.ToString()), "TestData");

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act - accessed by OWNER
        var result = await service.GetSecretAsync(secretId, ownerId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        
        // Should NOT be marked as Burned in Database
        var updatedSecret = await context.Secrets.FindAsync(secretId);
        Assert.NotNull(updatedSecret);
        Assert.False(updatedSecret.IsBurned);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldBurnOneTimeSecret_WhenAccessedBySomeoneElse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var user = new User { Id = ownerId, Email = "owner@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var secretId = Guid.NewGuid();
        var secret = new Secret
        {
            Id = secretId,
            OwnerId = ownerId,
            Comment = "One Time",
            IsOneTime = true,
            IsBurned = false,
            ContentType = "text/plain",
            FileName = "test.txt",
            IV = new byte[] { 1, 2, 3 },
            Size = 4,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        context.Secrets.Add(secret);
        await context.SaveChangesAsync();

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "Storage");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, secretId.ToString()), "TestData");

        var logger = NullLogger<SecretService>.Instance;
        var service = new SecretService(context, logger, CreateMockSettings());

        // Act - accessed by stranger (another user id)
        var result = await service.GetSecretAsync(secretId, strangerId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        
        // Should BE marked as Burned in Database
        var updatedSecret = await context.Secrets.FindAsync(secretId);
        Assert.NotNull(updatedSecret);
        Assert.True(updatedSecret.IsBurned);
    }
}
