using Pos.Domain.Common;
using Pos.Domain.Enums;
using Xunit;

namespace Pos.Domain.Tests.Common;

public class TenantAccessPolicyTests
{
    [Fact]
    public void ValidateAccess_WhenActive_ShouldReturnNull()
    {
        // Act
        var error = TenantAccessPolicy.ValidateAccess(TenantStatus.Active);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAccess_WhenSuspended_ShouldReturnForbiddenWithSuspendedCode()
    {
        // Act
        var error = TenantAccessPolicy.ValidateAccess(TenantStatus.Suspended);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal(TenantAccessPolicy.SuspendedCode, error.Code);
    }

    [Fact]
    public void ValidateAccess_WhenPendingProvisioning_ShouldReturnForbiddenWithNotActiveCode()
    {
        // Act
        var error = TenantAccessPolicy.ValidateAccess(TenantStatus.PendingProvisioning);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal(TenantAccessPolicy.NotActiveCode, error.Code);
    }

    [Fact]
    public void ValidateAccess_WhenNull_ShouldReturnForbiddenWithNotActiveCodeFailClosed()
    {
        // Act
        var error = TenantAccessPolicy.ValidateAccess(null);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal(TenantAccessPolicy.NotActiveCode, error.Code);
    }

    [Fact]
    public void ValidateAccess_WhenUnknownEnum_ShouldReturnForbiddenFailClosed()
    {
        // Act: valor de enum no definido (cast)
        var error = TenantAccessPolicy.ValidateAccess((TenantStatus)99);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal(TenantAccessPolicy.NotActiveCode, error.Code);
    }

    [Fact]
    public void ValidateAccess_CodesAreStable()
    {
        // Assert: los códigos son constantes estables
        Assert.Equal("Tenant.NotActive", TenantAccessPolicy.NotActiveCode);
        Assert.Equal("Tenant.Suspended", TenantAccessPolicy.SuspendedCode);
    }
}
