using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class UserTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateUserWithNullTenantIdShouldSucceedAndEmitEvent()
    {
        // Arrange
        string email = "superadmin@pos.com";
        string passwordHash = "ARGON2ID_HASH_SAMPLE";
        Guid roleId = Guid.NewGuid();

        // Act
        var user = User.Create(email, passwordHash, roleId, tenantId: null, firstName: "Super", lastName: "Admin", _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email.Value);
        Assert.Equal(roleId, user.RoleId);
        Assert.Null(user.TenantId);
        Assert.True(user.IsActive);
        Assert.Single(user.DomainEvents);
        Assert.IsType<DomainEvents.UserCreatedDomainEvent>(user.DomainEvents.First());
    }

    [Fact]
    public void CreateUserWithValidTenantIdShouldSucceed()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();

        // Act
        var user = User.Create("cajero@tenant.com", "HASH_SAMPLE", roleId, tenantId: tenantId, firstName: "Pedro", lastName: "Pérez", _timeProvider);

        // Assert
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(roleId, user.RoleId);
    }

    [Fact]
    public void CreateUserWithEmptyRoleIdShouldThrowDomainException()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            User.Create("admin@tenant.com", "HASH", Guid.Empty, tenantId, _timeProvider));

        Assert.Contains("RoleId", ex.Message);
    }

    [Fact]
    public void ChangeRole_WhenRoleIdChanges_ShouldEmitUserRoleChangedDomainEvent()
    {
        // Arrange
        Guid oldRoleId = Guid.NewGuid();
        Guid newRoleId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        var user = User.Create("user@tenant.com", "HASH", oldRoleId, tenantId, "Juan", "Pérez", _timeProvider);

        // Act
        user.ChangeRole(newRoleId, _timeProvider);

        // Assert
        Assert.Equal(newRoleId, user.RoleId);
        Assert.Equal(tenantId, user.TenantId);
        
        var roleChangedEvent = user.DomainEvents.OfType<DomainEvents.UserRoleChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(roleChangedEvent);
        Assert.Equal(user.Id, roleChangedEvent.UserId);
        Assert.Equal(oldRoleId, roleChangedEvent.OldRoleId);
        Assert.Equal(newRoleId, roleChangedEvent.NewRoleId);
        Assert.Equal(tenantId, roleChangedEvent.TenantId);
        Assert.True(roleChangedEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void ChangeRole_WhenRoleIdIsUnchanged_ShouldNotEmitUserRoleChangedDomainEvent()
    {
        // Arrange
        Guid roleId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        var user = User.Create("user@tenant.com", "HASH", roleId, tenantId, "Juan", "Pérez", _timeProvider);
        int eventCountBefore = user.DomainEvents.Count;

        // Act
        user.ChangeRole(roleId, _timeProvider);

        // Assert
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(eventCountBefore, user.DomainEvents.Count);
        Assert.Empty(user.DomainEvents.OfType<DomainEvents.UserRoleChangedDomainEvent>());
    }

    [Fact]
    public void ChangeRole_WithEmptyRoleId_ShouldThrowDomainException()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => user.ChangeRole(Guid.Empty, _timeProvider));
        Assert.Contains("RoleId", ex.Message);
    }

    [Fact]
    public void ChangeRole_WhenTenantUserAssignedSuperAdminRoleId_ShouldThrowDomainException()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => user.ChangeRole(Role.SuperAdminRoleId, _timeProvider));
        Assert.Contains("SuperAdmin", ex.Message);
    }

    [Fact]
    public void Activate_WhenNotActive_ShouldActivateAndEmitUserStatusChangedDomainEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);
        user.Deactivate(_timeProvider); // Set to inactive first
        user.ClearDomainEvents();

        // Act
        user.Activate(_timeProvider);

        // Assert
        Assert.True(user.IsActive);
        var statusEvent = user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(user.Id, statusEvent.UserId);
        Assert.Equal(user.TenantId, statusEvent.TenantId);
        Assert.True(statusEvent.IsActive);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotEmitEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);
        user.ClearDomainEvents();

        // Act
        user.Activate(_timeProvider);

        // Assert
        Assert.True(user.IsActive);
        Assert.Empty(user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>());
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateAndEmitUserStatusChangedDomainEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);
        user.ClearDomainEvents();

        // Act
        user.Deactivate(_timeProvider);

        // Assert
        Assert.False(user.IsActive);
        var statusEvent = user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(user.Id, statusEvent.UserId);
        Assert.Equal(user.TenantId, statusEvent.TenantId);
        Assert.False(statusEvent.IsActive);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldNotEmitEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez", _timeProvider);
        user.Deactivate(_timeProvider);
        user.ClearDomainEvents();

        // Act
        user.Deactivate(_timeProvider);

        // Assert
        Assert.False(user.IsActive);
        Assert.Empty(user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>());
    }

    [Fact]
    public void CreateAndUpdateUser_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        var roleChangedTime = new DateTimeOffset(2026, 10, 20, 16, 30, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        var oldRoleId = Guid.NewGuid();
        var newRoleId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act - Create
        var user = User.Create(new ValueObjects.Email("cajero@pos.com"), new ValueObjects.PasswordHash("HASH123"), oldRoleId, tenantId, "Carlos", "Gómez", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, user.CreatedAtUtc);
        var createdEvent = Assert.IsType<DomainEvents.UserCreatedDomainEvent>(Assert.Single(user.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, createdEvent.OccurredOnUtc);

        // Act - ChangeRole
        timeProvider.SetUtcNow(roleChangedTime);
        user.ChangeRole(newRoleId, timeProvider);

        // Assert - ChangeRole
        Assert.Equal(roleChangedTime.UtcDateTime, user.UpdatedAtUtc);
        var roleEvent = user.DomainEvents.OfType<DomainEvents.UserRoleChangedDomainEvent>().Single();
        Assert.Equal(roleChangedTime.UtcDateTime, roleEvent.OccurredOnUtc);
    }
}
