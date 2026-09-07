using Pos.Application.Common.Interfaces;
using Pos.Application.Purchases.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Purchases;

public class CompletePurchaseCommandHandlerTests
{
    private readonly FakePurchaseRepository _purchaseRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly CompletePurchaseCommandHandler _handler;

    public CompletePurchaseCommandHandlerTests()
    {
        _handler = new CompletePurchaseCommandHandler(_purchaseRepository, _unitOfWork, _dispatcher);
    }

    [Fact]
    public async Task HandleAsync_WithDraftPurchase_ShouldCompletePurchaseAndPublishDomainEvents()
    {
        // Arrange
        var purchase = Purchase.Create(
            Guid.NewGuid(),
            "PO-2026-010",
            new List<PurchaseLineItem>
            {
                PurchaseLineItem.Create(Guid.NewGuid(), "Insumo X", 5, Money.Create(20m, "USD"))
            },
            "USD"
        );
        _purchaseRepository.Purchases.Add(purchase);

        var command = new CompletePurchaseCommand(purchase.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PurchaseStatus.Completed.ToString(), result.Value.StatusName);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Single(_dispatcher.PublishedEvents);
    }

    [Fact]
    public async Task HandleAsync_WhenPurchaseAlreadyCompleted_ShouldReturnValidationError()
    {
        // Arrange
        var purchase = Purchase.Create(
            Guid.NewGuid(),
            "PO-2026-011",
            new List<PurchaseLineItem>
            {
                PurchaseLineItem.Create(Guid.NewGuid(), "Insumo Y", 2, Money.Create(50m, "USD"))
            },
            "USD"
        );
        purchase.Complete();
        _purchaseRepository.Purchases.Add(purchase);

        var command = new CompletePurchaseCommand(purchase.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Purchase.InvalidState", result.Error.Code);
    }

    private sealed class FakePurchaseRepository : IPurchaseRepository
    {
        public List<Purchase> Purchases { get; } = [];

        public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Purchases.FirstOrDefault(p => p.Id == id));
        public Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default) => Task.FromResult(Purchases.FirstOrDefault(p => p.OrderNumber == orderNumber));
        public Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default) { Purchases.Add(purchase); return Task.CompletedTask; }
        public void Update(Purchase purchase) { }
        public Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? supplierId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Purchase>, int)>((Purchases, Purchases.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }

    private sealed class FakeDispatcher : IDispatcher
    {
        public List<IDomainEvent> PublishedEvents { get; } = [];

        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default) where TDomainEvent : IDomainEvent
        {
            PublishedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
