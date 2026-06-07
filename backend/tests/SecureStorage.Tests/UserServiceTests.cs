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
    public async Task CreateInviteAsync_ShouldCreateInvite_WhenValid()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new UserService(context);
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

        var service = new UserService(context);

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

        var service = new UserService(context);

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

        var service = new UserService(context);

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
}
