using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class TenantGuardTests
{
    [Fact]
    public void RequireTenantId_WhenValidTenantId_ReturnsSameTenantId()
    {
        // Arrange
        var validTenantId = Guid.NewGuid();

        // Act
        var result = TenantGuard.RequireTenantId(validTenantId, "una entidad de prueba");

        // Assert
        Assert.Equal(validTenantId, result);
    }

    [Fact]
    public void RequireTenantId_WhenPlatformScopeTenantId_ThrowsDomainExceptionWithExpectedMessage()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            TenantGuard.RequireTenantId(PlatformScope.TenantId, "una entidad de prueba"));

        Assert.Equal("El ID del tenant es requerido para crear una entidad de prueba.", ex.Message);
    }

    [Fact]
    public void RequireTenantId_WhenGuidEmpty_ThrowsDomainExceptionWithExpectedMessage()
    {
        // Arrange & Act & Assert (Guid.Empty es equivalente a PlatformScope.TenantId)
        var ex = Assert.Throws<DomainException>(() =>
            TenantGuard.RequireTenantId(Guid.Empty, "una entidad de prueba"));

        Assert.Equal("El ID del tenant es requerido para crear una entidad de prueba.", ex.Message);
    }
}
