using Microsoft.Extensions.Logging;
using Pos.Application.Platform.Tenants.Commands.SuspendTenant;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class SuspendTenantCommandHandlerTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTenantStatusCacheInvalidator _cacheInvalidator = new();
    private readonly RecordingLogger<SuspendTenantCommandHandler> _logger = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly SuspendTenantCommandHandler _handler;

    public SuspendTenantCommandHandlerTests()
    {
        _handler = new SuspendTenantCommandHandler(
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
        var result = await _handler.HandleAsync(new SuspendTenantCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Tenant.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantIsActive_SuspendsAndPersists()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Active);
        tenant.ClearDomainEvents();

        // Act
        var result = await _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Single(tenant.DomainEvents.OfType<Pos.Domain.DomainEvents.TenantStatusChangedDomainEvent>());
    }

    [Fact]
    public async Task HandleAsync_WhenTenantIsActive_InvalidatesStatusCacheExactlyOnceWithTenantId()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Active);

        // Act
        var result = await _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, _cacheInvalidator.InvalidationCount);
        Assert.Equal(tenant.Id, Assert.Single(_cacheInvalidator.InvalidatedTenantIds));
    }

    [Fact]
    public async Task HandleAsync_WhenTenantIsPendingProvisioning_ReturnsInvalidTransitionConflict()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.PendingProvisioning);

        // Act
        var result = await _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TenantStatusTransitions.InvalidTransitionCode, result.Error.Code);
        Assert.Equal(TenantStatus.PendingProvisioning, tenant.Status);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantIsAlreadySuspended_ReturnsConflict()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Suspended);

        // Act
        var result = await _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(TenantStatusTransitions.AlreadySuspendedCode, result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceFails_DoesNotInvalidateStatusCache()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Active);
        _unitOfWork.ExceptionToThrowOnSave = new InvalidOperationException("fallo de persistencia");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None));

        // Assert
        Assert.Equal(0, _cacheInvalidator.InvalidationCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheInvalidationThrows_StillReturnsSuccessAndLogsError()
    {
        // Arrange
        var tenant = AddTenant(TenantStatus.Active);
        var cacheFailure = new InvalidOperationException("caché no disponible");
        _cacheInvalidator.ExceptionToThrow = cacheFailure;

        // Act
        var result = await _handler.HandleAsync(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _cacheInvalidator.InvalidationCount);

        var logEntry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(cacheFailure, logEntry.Exception);
        Assert.Contains(tenant.Id.ToString(), logEntry.Message);
    }
}
