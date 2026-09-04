using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.Commands;
using Pos.Application.Payments.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Payments;

public class ProcessPaymentCommandHandlerTests
{
    private readonly FakePaymentRepository _paymentRepository = new();
    private readonly FakeSaleRepository _saleRepository = new();
    private readonly FakePaymentGateway _paymentGateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly ProcessPaymentCommandHandler _handler;

    public ProcessPaymentCommandHandlerTests()
    {
        _handler = new ProcessPaymentCommandHandler(
            _paymentRepository,
            _saleRepository,
            _paymentGateway,
            _unitOfWork,
            _dispatcher);
    }

    [Fact]
    public async Task HandleAsync_WithValidSaleAndSuccessfulGateway_ShouldProcessPaymentAndReturnSuccess()
    {
        // Arrange
        var lineItem = SaleLineItem.Create(Guid.NewGuid(), "Producto Test", 1m, Money.Create(50m, "USD"));
        var sale = Sale.Create("V-001-0001", Guid.NewGuid(), null, new[] { lineItem }, 0m, "USD");
        _saleRepository.Sales.Add(sale);
        _paymentGateway.ShouldSucceed = true;

        var command = new ProcessPaymentCommand(
            sale.Id,
            Amount: 50m,
            Method: PaymentMethod.CreditCard,
            Currency: "USD",
            ExternalReference: "REF-12345");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Processed.ToString(), result.Value.StatusName);
        Assert.Single(_paymentRepository.Payments);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentSale_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
            SaleId: Guid.NewGuid(),
            Amount: 50m,
            Method: PaymentMethod.Cash);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Sale.NotFound", result.Error.Code);
        Assert.Empty(_paymentRepository.Payments);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba
    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public List<Payment> Payments { get; } = [];
        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Payments.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Payment>> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Payment>>(Payments.Where(p => p.SaleId == saleId).ToList());
        public Task AddAsync(Payment payment, CancellationToken cancellationToken = default) { Payments.Add(payment); return Task.CompletedTask; }
        public void Update(Payment payment) { }
    }

    private sealed class FakeSaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));
        public Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(s => s.ReceiptNumber.Equals(receiptNumber, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) { Sales.Add(sale); return Task.CompletedTask; }
        public void Update(Sale sale) { }
        public Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? sessionId, Guid? customerId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Sale>, int) >((Sales, Sales.Count));
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public bool ShouldSucceed { get; set; } = true;
        public Task<PaymentGatewayResult> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            if (ShouldSucceed)
                return Task.FromResult(new PaymentGatewayResult(true, "TXN-99999", null));
            return Task.FromResult(new PaymentGatewayResult(false, null, "FONDOS_INSUFICIENTES"));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }

    private sealed class FakeDispatcher : IDispatcher
    {
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default) where TDomainEvent : IDomainEvent => Task.CompletedTask;
    }
}
