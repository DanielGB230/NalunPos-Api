using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class UserTests
{
    [Fact]
    public void CreateUserWithNullTenantIdShouldSucceedAndEmitEvent()
    {
        // Arrange
        string email = "superadmin@pos.com";
        string passwordHash = "ARGON2ID_HASH_SAMPLE";
        Guid roleId = Guid.NewGuid();

        // Act
        var user = User.Create(email, passwordHash, roleId, tenantId: null, firstName: "Super", lastName: "Admin");

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
        var user = User.Create("cajero@tenant.com", "HASH_SAMPLE", roleId, tenantId: tenantId, firstName: "Pedro", lastName: "Pérez");

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
            User.Create("admin@tenant.com", "HASH", Guid.Empty, tenantId: tenantId));

        Assert.Contains("RoleId", ex.Message);
    }

    [Fact]
    public void ChangeRole_WhenRoleIdOrTenantIdChanges_ShouldEmitUserRoleChangedDomainEvent()
    {
        // Arrange
        Guid oldRoleId = Guid.NewGuid();
        Guid newRoleId = Guid.NewGuid();
        Guid oldTenantId = Guid.NewGuid();
        Guid newTenantId = Guid.NewGuid();
        var user = User.Create("user@tenant.com", "HASH", oldRoleId, oldTenantId, "Juan", "Pérez");

        // Act
        user.ChangeRole(newRoleId, newTenantId);

        // Assert
        Assert.Equal(newRoleId, user.RoleId);
        Assert.Equal(newTenantId, user.TenantId);
        
        var roleChangedEvent = user.DomainEvents.OfType<DomainEvents.UserRoleChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(roleChangedEvent);
        Assert.Equal(user.Id, roleChangedEvent.UserId);
        Assert.Equal(oldRoleId, roleChangedEvent.OldRoleId);
        Assert.Equal(newRoleId, roleChangedEvent.NewRoleId);
        Assert.Equal(oldTenantId, roleChangedEvent.OldTenantId);
        Assert.Equal(newTenantId, roleChangedEvent.NewTenantId);
        Assert.True(roleChangedEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void ChangeRole_WhenRoleIdAndTenantIdAreUnchanged_ShouldNotEmitUserRoleChangedDomainEvent()
    {
        // Arrange
        Guid roleId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        var user = User.Create("user@tenant.com", "HASH", roleId, tenantId, "Juan", "Pérez");
        int eventCountBefore = user.DomainEvents.Count;

        // Act
        user.ChangeRole(roleId, tenantId);

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
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez");

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => user.ChangeRole(Guid.Empty, Guid.NewGuid()));
        Assert.Contains("RoleId", ex.Message);
    }

    [Fact]
    public void Activate_WhenNotActive_ShouldActivateAndEmitUserStatusChangedDomainEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez");
        user.Deactivate(); // Set to inactive first
        user.ClearDomainEvents();

        // Act
        user.Activate();

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
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez");
        user.ClearDomainEvents();

        // Act
        user.Activate();

        // Assert
        Assert.True(user.IsActive);
        Assert.Empty(user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>());
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateAndEmitUserStatusChangedDomainEvent()
    {
        // Arrange
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez");
        user.ClearDomainEvents();

        // Act
        user.Deactivate();

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
        var user = User.Create("user@tenant.com", "HASH", Guid.NewGuid(), Guid.NewGuid(), "Juan", "Pérez");
        user.Deactivate();
        user.ClearDomainEvents();

        // Act
        user.Deactivate();

        // Assert
        Assert.False(user.IsActive);
        Assert.Empty(user.DomainEvents.OfType<DomainEvents.UserStatusChangedDomainEvent>());
    }
}
