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
}
