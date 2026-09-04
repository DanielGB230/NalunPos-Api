using Pos.Application.Common.Interfaces;
using Pos.Application.Sales.Commands;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Sales;

public class CreateSaleCommandHandlerTests
{
    private readonly FakeSaleRepository _saleRepository = new();
    private readonly FakeCashRegisterRepository _registerRepository = new();
    private readonly FakeCustomerRepository _customerRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly CreateSaleCommandHandler _handler;

    public CreateSaleCommandHandlerTests()
    {
        _handler = new CreateSaleCommandHandler(
            _saleRepository,
            _registerRepository,
            _customerRepository,
            _unitOfWork,
            _dispatcher);
    }

    [Fact]
    public async Task HandleAsync_WithOpenSessionAndValidLineItems_ShouldCreateSaleAndReturnSuccessResult()
    {
        // Arrange
        var session = CashRegisterSession.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Create(100m, "USD"),
            "Apertura inicial");
        _registerRepository.Sessions.Add(session);

        var command = new CreateSaleCommand(
            "V-001-0001",
            session.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(Guid.NewGuid(), "Producto A", 2, 50m)
            },
            TaxRatePercentage: 18m,
            Currency: "USD");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("V-001-0001", result.Value.ReceiptNumber);
        Assert.Equal(118m, result.Value.TotalAmount); // 100 + 18% tax
        Assert.Single(_saleRepository.Sales);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithClosedCashRegisterSession_ShouldReturnConflictResult()
    {
        // Arrange
        var session = CashRegisterSession.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Create(100m, "USD"),
            "Apertura previa");
        session.Close(Money.Create(100m, "USD"), Money.Create(100m, "USD"), "Cierre de sesión");
        _registerRepository.Sessions.Add(session);

        var command = new CreateSaleCommand(
            "V-001-0002",
            session.Id,
            CustomerId: null,
            LineItems: new List<CreateSaleItemDto>
            {
                new(Guid.NewGuid(), "Producto B", 1, 20m)
            });

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("CashRegisterSession.Closed", result.Error.Code);
        Assert.Empty(_saleRepository.Sales);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba
    private sealed class FakeSaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));
        }

        public Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Sales.FirstOrDefault(s => s.ReceiptNumber.Equals(receiptNumber, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public void Update(Sale sale)
        {
        }

        public Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? sessionId, Guid? customerId, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default)
        {
            return Task.FromResult< (IReadOnlyList<Sale>, int) >((Sales.AsReadOnly(), Sales.Count));
        }
    }

    private sealed class FakeCashRegisterRepository : ICashRegisterRepository
    {
        public List<CashRegisterSession> Sessions { get; } = [];

        public Task<CashRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CashRegister?>(null);
        public Task<IReadOnlyList<CashRegister>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CashRegister>>([]);
        public Task AddAsync(CashRegister register, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(CashRegister register) { }
        public Task<CashRegisterSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));
        public Task<CashRegisterSession?> GetActiveSessionByRegisterIdAsync(Guid registerId, CancellationToken cancellationToken = default) => Task.FromResult(Sessions.FirstOrDefault(s => s.CashRegisterId == registerId && s.Status == SessionStatus.Open));
        public Task AddSessionAsync(CashRegisterSession session, CancellationToken cancellationToken = default) { Sessions.Add(session); return Task.CompletedTask; }
        public void UpdateSession(CashRegisterSession session) { }
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(Customer customer) { }
        public void Delete(Customer customer) { }
        public Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, CancellationToken cancellationToken = default) => Task.FromResult< (IReadOnlyList<Customer>, int) >(([], 0));
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
