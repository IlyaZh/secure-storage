using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;
using SecureStorage.Domain.Services;
using Xunit;

namespace SecureStorage.Tests;

public class UserServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var email = "test@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new UserService(context);

        // Act
        var result = await service.GetByEmailAsync("  TEST@example.com  ", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new UserService(context);

        // Act
        var result = await service.GetByEmailAsync("nonexistent@example.com", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterWithInviteAsync_ShouldSucceed_WhenInviteIsValid()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var inviteId = Guid.NewGuid();
        var email = "newuser@example.com";
        var invite = new Invite
        {
            Id = inviteId,
            Email = email,
            IsUsed = false,
            IssuedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        var service = new UserService(context);

        // Act
        var result = await service.RegisterWithInviteAsync(email, inviteId, CancellationToken.None);

        // Assert
        Assert.Equal(RegistrationResult.Success, result);
        
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        
        var updatedInvite = await context.Invites.FindAsync(inviteId);
        Assert.True(updatedInvite!.IsUsed);
        Assert.NotNull(updatedInvite.UsedAt);
    }

    [Fact]
    public async Task RegisterWithInviteAsync_ShouldFail_WhenInviteDoesNotExist()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new UserService(context);

        // Act
        var result = await service.RegisterWithInviteAsync("user@example.com", Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Equal(RegistrationResult.InviteNotFoundOrUsed, result);
    }

    [Fact]
    public async Task RegisterWithInviteAsync_ShouldFail_WhenInviteEmailMismatches()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var inviteId = Guid.NewGuid();
        var invite = new Invite
        {
            Id = inviteId,
            Email = "invitee@example.com",
            IsUsed = false,
            IssuedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        var service = new UserService(context);

        // Act
        var result = await service.RegisterWithInviteAsync("wrongemail@example.com", inviteId, CancellationToken.None);

        // Assert
        Assert.Equal(RegistrationResult.EmailMismatch, result);
    }

    [Fact]
    public async Task RegisterWithInviteAsync_ShouldFail_WhenUserIsAlreadyRegistered()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var email = "existing@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        var inviteId = Guid.NewGuid();
        var invite = new Invite
        {
            Id = inviteId,
            Email = email,
            IsUsed = false,
            IssuedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        var service = new UserService(context);

        // Act
        var result = await service.RegisterWithInviteAsync(email, inviteId, CancellationToken.None);

        // Assert
        Assert.Equal(RegistrationResult.AlreadyRegistered, result);
    }


    [Fact]
    public async Task GetStorageUsageAsync_ShouldCalculateActiveSecretsSizeSum()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);

        // Active secret
        context.Secrets.Add(new Secret
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Comment = "Active",
            Size = 500,
            IsBurned = false,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            ContentType = "text/plain",
            FileName = "",
            IV = new byte[0]
        });

        // Expired secret (should be ignored)
        context.Secrets.Add(new Secret
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Comment = "Expired",
            Size = 1000,
            IsBurned = false,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            ContentType = "text/plain",
            FileName = "",
            IV = new byte[0]
        });

        // Burned secret (should be ignored)
        context.Secrets.Add(new Secret
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Comment = "Burned",
            Size = 2000,
            IsBurned = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            ContentType = "text/plain",
            FileName = "",
            IV = new byte[0]
        });

        await context.SaveChangesAsync();

        var service = new UserService(context);

        // Act
        var result = await service.GetStorageUsageAsync(userId, CancellationToken.None);

        // Assert
        Assert.Equal(500L, result.UsedBytes);
        Assert.Equal(200L * 1024 * 1024, result.QuotaBytes);
    }
}
