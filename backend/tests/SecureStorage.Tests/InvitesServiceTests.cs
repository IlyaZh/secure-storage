using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using SecureStorage.Domain.Settings;
using Xunit;

namespace SecureStorage.Tests;

public class InvitesServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private class MockOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value => value;
        public T Get(string? name) => value;
    }

    private IOptionsSnapshot<InvitesCleanupWorkerSettings> CreateMockSettings(int ttlHours)
    {
        return new MockOptionsSnapshot<InvitesCleanupWorkerSettings>(new InvitesCleanupWorkerSettings
        {
            TTLh = ttlHours
        });
    }

    [Fact]
    public async Task CreateInviteAsync_ShouldCreateInvite_WhenValid()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var settings = CreateMockSettings(120);
        var service = new InvitesService(context, settings);
        var issuerId = Guid.NewGuid();
        var targetEmail = "invitee@example.com";

        // Act
        var invite = await service.CreateInviteAsync(issuerId, targetEmail, CancellationToken.None);

        // Assert
        Assert.NotNull(invite);
        Assert.Equal(targetEmail, invite.Email);
        Assert.False(invite.IsUsed);
        Assert.Equal(issuerId, invite.IssuedByUserId);

        var dbInvite = await context.Invites.FindAsync(invite.Id);
        Assert.NotNull(dbInvite);
    }

    [Fact]
    public async Task CreateInviteAsync_ShouldThrowException_WhenUserAlreadyExists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var email = "existing@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var settings = CreateMockSettings(120);
        var service = new InvitesService(context, settings);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateInviteAsync(Guid.NewGuid(), email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateInviteAsync_ShouldThrowException_WhenActiveInviteAlreadyExists()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var email = "invited@example.com";
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Email = email,
            IsUsed = false,
            IssuedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        context.Invites.Add(invite);
        await context.SaveChangesAsync();

        var settings = CreateMockSettings(120);
        var service = new InvitesService(context, settings);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateInviteAsync(Guid.NewGuid(), email, CancellationToken.None));
    }

    [Fact]
    public async Task GetUserInvitesAsync_ShouldReturnCorrectInvitesAndOrder()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();

        // Create 25 invites from this user
        for (int i = 0; i < 25; i++)
        {
            var invite = new Invite
            {
                Id = Guid.CreateVersion7(),
                Email = $"user{i}@example.com",
                IsUsed = false,
                IssuedByUserId = userId,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            };
            context.Invites.Add(invite);
        }

        // Add 5 invites from another user
        for (int i = 0; i < 5; i++)
        {
            var invite = new Invite
            {
                Id = Guid.CreateVersion7(),
                Email = $"other{i}@example.com",
                IsUsed = false,
                IssuedByUserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            context.Invites.Add(invite);
        }

        await context.SaveChangesAsync();

        var settings = CreateMockSettings(120);
        var service = new InvitesService(context, settings);

        // Act: Fetch first page (max 20)
        var page1 = await service.GetUserInvitesAsync(userId, null, CancellationToken.None);

        // Assert
        Assert.Equal(20, page1.Count);
        
        // Ensure they are ordered descending by ID
        for (int i = 0; i < page1.Count - 1; i++)
        {
            Assert.True(page1[i].Id.CompareTo(page1[i+1].Id) > 0);
        }

        // Act: Fetch second page using cursor
        var lastId = page1[^1].Id;
        var page2 = await service.GetUserInvitesAsync(userId, lastId, CancellationToken.None);

        // Assert
        Assert.Equal(5, page2.Count);
    }

    [Fact]
    public async Task CleanupExpiredInvitesBatchAsync_ShouldCleanupOnlyExpiredAndUnused()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var issuerId = Guid.NewGuid();
        var settings = CreateMockSettings(10); // TTL is 10 hours
        var service = new InvitesService(context, settings);

        var now = DateTime.UtcNow;

        // 1. Expired, unused -> should be cleaned up
        var expiredUnused = new Invite
        {
            Id = Guid.CreateVersion7(),
            Email = "expired_unused@example.com",
            IsUsed = false,
            IssuedByUserId = issuerId,
            CreatedAt = now.AddHours(-11)
        };

        // 2. Expired, used -> should NOT be cleaned up
        var expiredUsed = new Invite
        {
            Id = Guid.CreateVersion7(),
            Email = "expired_used@example.com",
            IsUsed = true,
            UsedAt = now.AddHours(-1),
            IssuedByUserId = issuerId,
            CreatedAt = now.AddHours(-11)
        };

        // 3. Not expired, unused -> should NOT be cleaned up
        var activeUnused = new Invite
        {
            Id = Guid.CreateVersion7(),
            Email = "active_unused@example.com",
            IsUsed = false,
            IssuedByUserId = issuerId,
            CreatedAt = now.AddHours(-9)
        };

        context.Invites.AddRange(expiredUnused, expiredUsed, activeUnused);
        await context.SaveChangesAsync();

        // Act
        var count = await service.CleanupExpiredInvitesBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(1, count);

        // Verify database state
        var dbExpiredUnused = await context.Invites.FindAsync(expiredUnused.Id);
        var dbExpiredUsed = await context.Invites.FindAsync(expiredUsed.Id);
        var dbActiveUnused = await context.Invites.FindAsync(activeUnused.Id);

        Assert.Null(dbExpiredUnused);
        Assert.NotNull(dbExpiredUsed);
        Assert.NotNull(dbActiveUnused);
    }
}
