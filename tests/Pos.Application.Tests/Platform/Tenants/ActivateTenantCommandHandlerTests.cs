using Microsoft.Extensions.Logging;
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
    private readonly FakeTenantStatusCacheInvalidator _cacheInvalidator = new();
    private readonly RecordingLogger<ActivateTenantCommandHandler> _logger = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ActivateTenantCommandHandler _handler;

    public ActivateTenantCommandHandlerTests()
    {
        _handler = new ActivateTenantCommandHandler(
            _tenantRepository,
            _unitOfWork,
            _cacheInvalidator,
            _logger,
            _timeProvider);
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
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
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

    [Theory]
    [InlineData(TenantStatus.PendingProvisioning)]
    [InlineData(TenantStatus.Suspended)]
    public async Task HandleAsync_WhenTenantCanBeActivated_InvalidatesStatusCacheExactlyOnceWithTenantId(
        TenantStatus initialStatus)
    {
        // Arrange
        var tenant = AddTenant(initialStatus);

        // Act
        var result = await _handler.HandleAsync(new ActivateTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, _cacheInvalidator.InvalidationCount);
        Assert.Equal(tenant.Id, Assert.Single(_cacheInvalidator.InvalidatedTenantIds));
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
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_DoesNotInvalidateStatusCache()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Suspended);
        _unitOfWork.ExceptionToThrowOnSave = new InvalidOperationException("fallo de persistencia");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(new ActivateTenantCommand(tenant.Id), CancellationToken.None));

        // Assert
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheInvalidationThrows_StillReturnsSuccessAndLogsError()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Suspended);
        var cacheFailure = new InvalidOperationException("caché no disponible");
        _cacheInvalidator.ExceptionToThrow = cacheFailure;

        // Act
        var result = await _handler.HandleAsync(new ActivateTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _cacheInvalidator.InvalidationCount);

        var logEntry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(cacheFailure, logEntry.Exception);
        Assert.Contains(tenant.Id.ToString(), logEntry.Message);
    }
}
