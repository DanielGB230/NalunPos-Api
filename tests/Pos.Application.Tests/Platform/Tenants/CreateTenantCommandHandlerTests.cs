using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class CreateTenantCommandHandlerTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeBranchRepository _branchRepository = new();
    private readonly FakeWarehouseRepository _warehouseRepository = new();
    private readonly FakeRoleRepository _roleRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateTenantCommandHandler _handler;

    public CreateTenantCommandHandlerTests()
    {
        _handler = new CreateTenantCommandHandler(
            _tenantRepository,
            _userRepository,
            _branchRepository,
            _warehouseRepository,
            _roleRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidData_ShouldCreateTenantAndTenantAdminUserAndReturnGuid()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "Empresa POS Demo S.A.C.",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        
        Assert.Single(_tenantRepository.Tenants);
        Assert.Equal("Empresa POS Demo S.A.C.", _tenantRepository.Tenants[0].Name);
        Assert.Equal("20601234567", _tenantRepository.Tenants[0].TaxId.Value);

        Assert.Single(_userRepository.Users);
        var createdUser = _userRepository.Users[0];
        Assert.Equal("admin@demopos.com", createdUser.Email.Value);
        Assert.NotEqual(Guid.Empty, createdUser.RoleId);
        Assert.Equal(result.Value, createdUser.TenantId);
        Assert.Equal("HASHED_Password123!", createdUser.PasswordHash.Value);

        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateDocumentNumber_ShouldReturnConflictError()
    {
        // Arrange
        var existingTenant = Tenant.Create("Empresa Existente", "20601234567");
        _tenantRepository.Tenants.Add(existingTenant);

        var command = new CreateTenantCommand(
            "Nueva Empresa S.A.",
            "20601234567",
            "admin@nuevaempresa.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Tenant.AlreadyExists", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateAdminEmail_ShouldReturnConflictError()
    {
        // Arrange
        var existingTenant = Tenant.Create("Tenant 1", "20609999999");
        var existingUser = User.Create(new Email("admin@demopos.com"), new PasswordHash("HASH"), Guid.NewGuid(), existingTenant.Id);
        _userRepository.Users.Add(existingUser);

        var command = new CreateTenantCommand(
            "Empresa POS Demo 2",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.EmailAlreadyExists", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidDomainData_ShouldReturnValidationError()
    {
        // Arrange (nombre de tenant en blanco invalida la regla de dominio)
        var command = new CreateTenantCommand(
            "   ",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Tenant.Invalid", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba
    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Tenants { get; } = [];

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
        }

        public Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
        {
            string normalized = taxId.Trim();
            return Task.FromResult(Tenants.Any(t => t.TaxId.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public void Update(Tenant tenant)
        {
        }

        public Task<(IReadOnlyList<Pos.Application.Platform.Tenants.DTOs.TenantDto> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            CancellationToken cancellationToken = default)
        {
            var dtos = Tenants
                .Select(t => new Pos.Application.Platform.Tenants.DTOs.TenantDto(t.Id, t.Name, t.TaxId.Value, t.Status.ToString(), t.CreatedAtUtc))
                .ToList();

            return Task.FromResult<(IReadOnlyList<Pos.Application.Platform.Tenants.DTOs.TenantDto>, int)>((dtos, dtos.Count));
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            string normalized = email.Trim().ToLowerInvariant();
            return Task.FromResult(Users.FirstOrDefault(u => u.Email.Value == normalized));
        }

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            string normalized = email.Trim().ToLowerInvariant();
            return Task.FromResult(Users.Any(u => u.Email.Value == normalized && (excludeId == null || u.Id != excludeId.Value)));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public void Update(User user)
        {
        }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive, CancellationToken cancellationToken = default)
        {
            return Task.FromResult< (IReadOnlyList<User>, int) >((Users.AsReadOnly(), Users.Count));
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"HASHED_{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"HASHED_{password}";

        public bool Verify(string password, PasswordHash passwordHash) => passwordHash.Value == $"HASHED_{password}";
    }

    private sealed class FakeBranchRepository : IBranchRepository
    {
        public List<Branch> Branches { get; } = [];
        public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Branches.FirstOrDefault(b => b.Id == id));
        public Task<IReadOnlyList<Branch>> GetAllAsync(bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Branch>>(Branches);
        public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Branch branch, CancellationToken cancellationToken = default) { Branches.Add(branch); return Task.CompletedTask; }
        public void Update(Branch branch) { }
    }

    private sealed class FakeWarehouseRepository : IWarehouseRepository
    {
        public List<Warehouse> Warehouses { get; } = [];
        public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default) { Warehouses.Add(warehouse); return Task.CompletedTask; }
        public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.Id == id));
        public Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.FirstOrDefault(w => w.IsDefault));
        public Task<IReadOnlyList<Warehouse>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Warehouse>>(Warehouses);
        public Task<int> CountByTenantAsync(CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.Count);
        public void Update(Warehouse warehouse) { }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public List<Role> Roles { get; } = [];
        public Task AddAsync(Role role, CancellationToken cancellationToken = default) { Roles.Add(role); return Task.CompletedTask; }
        public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Roles.Any(r => r.Name == name && r.Id != excludeId));
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Roles.FirstOrDefault(r => r.Id == id));
        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Roles.FirstOrDefault(r => r.Name == name));
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Role>>(Roles);
        public void Update(Role role) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
