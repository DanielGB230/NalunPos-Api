using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class UserTests
{
    [Fact]
    public void CreateSuperAdminWithNullTenantIdShouldSucceedAndEmitEvent()
    {
        // Arrange
        string email = "superadmin@pos.com";
        string passwordHash = "ARGON2ID_HASH_SAMPLE";

        // Act
        var user = User.Create(email, passwordHash, UserRole.SuperAdmin, tenantId: null, firstName: "Super", lastName: "Admin");

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email.Value);
        Assert.Equal(UserRole.SuperAdmin, user.Role);
        Assert.Null(user.TenantId);
        Assert.True(user.IsActive);
        Assert.Single(user.DomainEvents);
        Assert.IsType<DomainEvents.UserCreatedDomainEvent>(user.DomainEvents.First());
    }

    [Fact]
    public void CreateTenantUserWithValidTenantIdShouldSucceed()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        // Act
        var user = User.Create("cajero@tenant.com", "HASH_SAMPLE", UserRole.Cajero, tenantId: tenantId, firstName: "Pedro", lastName: "Pérez");

        // Assert
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(UserRole.Cajero, user.Role);
    }

    [Fact]
    public void CreateSuperAdminWithNonNullTenantIdShouldThrowDomainException()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            User.Create("superadmin@pos.com", "HASH", UserRole.SuperAdmin, tenantId: tenantId));

        Assert.Contains("SuperAdmin", ex.Message);
    }

    [Fact]
    public void CreateTenantAdminWithNullTenantIdShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            User.Create("admin@tenant.com", "HASH", UserRole.TenantAdmin, tenantId: null));

        Assert.Contains("TenantId obligatorio", ex.Message);
    }

    [Fact]
    public void CreateCajeroWithEmptyTenantIdShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            User.Create("cajero@tenant.com", "HASH", UserRole.Cajero, tenantId: Guid.Empty));

        Assert.Contains("TenantId obligatorio", ex.Message);
    }
}
