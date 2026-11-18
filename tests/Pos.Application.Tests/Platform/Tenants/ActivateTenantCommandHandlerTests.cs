using Pos.Application.Platform.Tenants.Commands.ActivateTenant;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class ActivateTenantCommandHandlerTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ActivateTenantCommandHandler _handler;

    public ActivateTenantCommandHandlerTests()
    {
        _handler = new ActivateTenantCommandHandler(_tenantRepository, _unitOfWork, _timeProvider);
    }

    private Tenant AddTenant(TenantStatus status)
    {
        var tenant = Tenant.Create(
            $"Empresa {Guid.NewGuid():N}",
            $"T{Guid.NewGuid():N}"[..12],
            _timeProvider);

        if (status is TenantStatus.Active or TenantStatus.Suspended)
        {
            tenant.Activate(_timeProvider);
        }

        if (status == TenantStatus.Suspended)
        {
            tenant.Suspend(_timeProvider);
        }

        _tenantRepository.Tenants.Add(tenant);
        return tenant;
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        // Act
        var result = await _handler.HandleAsync(new ActivateTenantCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Tenant.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(TenantStatus.PendingProvisioning)]
    [InlineData(TenantStatus.Suspended)]
    public async Task HandleAsync_WhenTenantCanBeActivated_ActivatesAndPersists(TenantStatus initialStatus)
    {
        // Arrange
        var tenant = AddTenant(initialStatus);
        tenant.ClearDomainEvents();

        // Act
        var result = await _handler.HandleAsync(new ActivateTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Single(tenant.DomainEvents.OfType<Pos.Domain.DomainEvents.TenantStatusChangedDomainEvent>());
    }

    [Fact]
    public async Task HandleAsync_WhenTenantIsAlreadyActive_ReturnsConflict()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Active);

        // Act
        var result = await _handler.HandleAsync(new ActivateTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TenantStatusTransitions.AlreadyActiveCode, result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }
}
