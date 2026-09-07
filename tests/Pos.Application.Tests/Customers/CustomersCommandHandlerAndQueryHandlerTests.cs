using Pos.Application.Common.Interfaces;
using Pos.Application.Customers.Commands;
using Pos.Application.Customers.Queries;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Customers;

public class CustomersCommandHandlerAndQueryHandlerTests
{
    private readonly FakeCustomerRepository _customerRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task GetCustomerById_WhenCustomerExists_ShouldReturnSuccess()
    {
        // Arrange
        var customer = Customer.Create(
            "Cliente Frecuente",
            TaxId.Create("20123456789", "PE"),
            "cliente@test.com",
            "+51999888777",
            Address.Create("Av. Principal 100", "Lima", "15001", "PE")
        );
        _customerRepository.Customers.Add(customer);

        var query = new GetCustomerByIdQuery(customer.Id);
        var handler = new GetCustomerByIdQueryHandler(_customerRepository);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Cliente Frecuente", result.Value.FullName);
    }

    [Fact]
    public async Task GetCustomerById_WhenCustomerDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var query = new GetCustomerByIdQuery(Guid.NewGuid());
        var handler = new GetCustomerByIdQueryHandler(_customerRepository);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Customer.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_ShouldCreateCustomerAndReturnSuccess()
    {
        // Arrange
        var command = new CreateCustomerCommand(
            "Empresa SAC",
            "20987654321",
            "PE",
            "contacto@empresa.pe",
            "+51912345678",
            "Calle Comercial 456",
            "Lima",
            "15002",
            "PE"
        );
        var handler = new CreateCustomerCommandHandler(_customerRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Empresa SAC", result.Value.FullName);
        Assert.Single(_customerRepository.Customers);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateCustomer_WhenTaxIdAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingCustomer = Customer.Create(
            "Empresa Existente",
            TaxId.Create("20987654321", "PE"),
            "existente@test.pe",
            "+51900000000"
        );
        _customerRepository.Customers.Add(existingCustomer);

        var command = new CreateCustomerCommand(
            "Nueva Empresa",
            "20987654321",
            "PE"
        );
        var handler = new CreateCustomerCommandHandler(_customerRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Customer.AlreadyExists", result.Error.Code);
        Assert.Single(_customerRepository.Customers);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));
        public Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default) => Task.FromResult(Customers.FirstOrDefault(c => c.TaxId.Value == taxId.Value));
        public Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Customers.Any(c => c.TaxId.Value == taxId.Value && c.Id != excludeId));
        public Task AddAsync(Customer customer, CancellationToken cancellationToken = default) { Customers.Add(customer); return Task.CompletedTask; }
        public void Update(Customer customer) { }
        public void Delete(Customer customer) => Customers.Remove(customer);
        public Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Customer>, int)>((Customers, Customers.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
