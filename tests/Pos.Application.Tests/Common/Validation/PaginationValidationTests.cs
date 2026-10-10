using Pos.Application.Tests.Support.Fakes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Common.Validation;
using Pos.Application.Inventory.DTOs;
using Pos.Application.Inventory.Queries;
using Pos.Application.Users.DTOs;
using Pos.Application.Users.Queries;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

public class PaginationValidationTests
{
    private static readonly Type AssemblyType = typeof(DependencyInjection);

    [Fact]
    public void All_Paginated_Queries_Must_Have_An_IValidator_Registered()
    {
        var paginatedQueryTypes = GetPaginatedQueryTypes();

        Assert.True(paginatedQueryTypes.Count >= 14, $"Se esperaban al menos 14 queries paginadas, pero se encontraron {paginatedQueryTypes.Count}.");

        var appAssembly = AssemblyType.Assembly;
        var validatorTypes = appAssembly.GetTypes()
            .Where(t => t.BaseType is { IsGenericType: true } && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .ToList();

        var missingValidators = new List<string>();

        foreach (var queryType in paginatedQueryTypes)
        {
            bool hasValidator = validatorTypes.Any(vt =>
                vt.BaseType!.GetGenericArguments()[0] == queryType);

            if (!hasValidator)
            {
                missingValidators.Add(queryType.Name);
            }
        }

        Assert.Empty(missingValidators);
    }

    [Fact]
    public async Task All_Paginated_Query_Validators_Reject_Invalid_PageNumber_And_PageSize()
    {
        var paginatedQueryTypes = GetPaginatedQueryTypes();
        var appAssembly = AssemblyType.Assembly;

        foreach (var queryType in paginatedQueryTypes)
        {
            var validatorType = appAssembly.GetTypes()
                .First(t => t.BaseType is { IsGenericType: true } && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>) && t.BaseType.GetGenericArguments()[0] == queryType);

            var validator = (IValidator)Activator.CreateInstance(validatorType)!;

            // (1) PageNumber = 0, PageSize = 10 -> debe fallar ÚNICAMENTE en PageNumber
            var queryPageNumZero = CreateQueryWithPaginationInstance(queryType, pageNumber: 0, pageSize: 10);
            var resultPageNumZero = await validator.ValidateAsync(new ValidationContext<object>(queryPageNumZero));
            Assert.False(resultPageNumZero.IsValid, $"El validador de '{queryType.Name}' debe rechazar PageNumber = 0.");
            Assert.All(resultPageNumZero.Errors, err => Assert.Equal("PageNumber", err.PropertyName));

            // (2) PageNumber = 1, PageSize = 0 -> debe fallar ÚNICAMENTE en PageSize
            var queryPageSizeZero = CreateQueryWithPaginationInstance(queryType, pageNumber: 1, pageSize: 0);
            var resultPageSizeZero = await validator.ValidateAsync(new ValidationContext<object>(queryPageSizeZero));
            Assert.False(resultPageSizeZero.IsValid, $"El validador de '{queryType.Name}' debe rechazar PageSize = 0.");
            Assert.All(resultPageSizeZero.Errors, err => Assert.Equal("PageSize", err.PropertyName));

            // (3) PageNumber = 1, PageSize = 101 -> debe fallar ÚNICAMENTE en PageSize
            var queryPageSize101 = CreateQueryWithPaginationInstance(queryType, pageNumber: 1, pageSize: 101);
            var resultPageSize101 = await validator.ValidateAsync(new ValidationContext<object>(queryPageSize101));
            Assert.False(resultPageSize101.IsValid, $"El validador de '{queryType.Name}' debe rechazar PageSize = 101.");
            Assert.All(resultPageSize101.Errors, err => Assert.Equal("PageSize", err.PropertyName));

            // (4) PageNumber = 1, PageSize = 50 -> válido (0 errores)
            var queryValid = CreateQueryWithPaginationInstance(queryType, pageNumber: 1, pageSize: 50);
            var resultValid = await validator.ValidateAsync(new ValidationContext<object>(queryValid));
            Assert.True(resultValid.IsValid, $"El validador de '{queryType.Name}' debe aceptar PageNumber = 1, PageSize = 50. Errores: {string.Join(", ", resultValid.Errors.Select(e => e.ErrorMessage))}");
        }
    }

    [Fact]
    public async Task Container_Resolved_GetUsersQuery_ValidPageSize_ReachesHandlerUnclamped()
    {
        var services = new ServiceCollection();
        var currentUserService = new FakeCurrentUserService { UserId = Guid.NewGuid() };
        var userRepository = new FakeUserRepository();
        var currentUserPermissions = new FakeCurrentUserPermissions();

        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddSingleton<IUserRepository>(userRepository);
        services.AddSingleton<ICurrentUserPermissions>(currentUserPermissions);

        services.AddApplicationServices();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IQueryHandler<GetUsersQuery, PagedResult<UserDto>>>();

        userRepository.UserToReturn = User.Create("test@example.com", "hash", Guid.NewGuid(), Guid.NewGuid(), "Admin", "User");

        var validQuery = new GetUsersQuery(PageNumber: 1, PageSize: 50);
        var pagedResult = await handler.HandleAsync(validQuery);

        Assert.Equal(50, userRepository.LastRequestedPageSize);
        Assert.Equal(50, pagedResult.PageSize);
    }

    [Fact]
    public async Task Container_Resolved_GetUsersQuery_InvalidPageSize_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        var currentUserService = new FakeCurrentUserService { UserId = Guid.NewGuid() };
        var userRepository = new FakeUserRepository();
        var currentUserPermissions = new FakeCurrentUserPermissions();

        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddSingleton<IUserRepository>(userRepository);
        services.AddSingleton<ICurrentUserPermissions>(currentUserPermissions);

        services.AddApplicationServices();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IQueryHandler<GetUsersQuery, PagedResult<UserDto>>>();

        userRepository.UserToReturn = User.Create("test@example.com", "hash", Guid.NewGuid(), Guid.NewGuid(), "Admin", "User");

        var invalidQuery = new GetUsersQuery(PageNumber: 1, PageSize: 101);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(invalidQuery));
    }

    [Fact]
    public async Task Container_Resolved_GetInventoryHistoryQuery_InvalidPageSize_ReturnsResultFailWithFieldErrors()
    {
        var services = new ServiceCollection();
        var currentUserService = new FakeCurrentUserService { UserId = Guid.NewGuid() };
        var userRepository = new FakeUserRepository();
        var inventoryRepository = new FakeInventoryRepository();
        var productRepository = new FakeProductRepository();
        var currentUserPermissions = new FakeCurrentUserPermissions();

        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddSingleton<IUserRepository>(userRepository);
        services.AddSingleton<IInventoryRepository>(inventoryRepository);
        services.AddSingleton<IProductRepository>(productRepository);
        services.AddSingleton<ICurrentUserPermissions>(currentUserPermissions);

        services.AddApplicationServices();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IQueryHandler<GetInventoryHistoryQuery, Result<PagedResult<InventoryMovementDto>>>>();

        userRepository.UserToReturn = User.Create("test@example.com", "hash", Guid.NewGuid(), Guid.NewGuid(), "Admin", "User");

        var invalidQuery = new GetInventoryHistoryQuery(ProductId: Guid.NewGuid(), PageNumber: 1, PageSize: 101);

        var result = await handler.HandleAsync(invalidQuery);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Error", result.Error.Code);
        Assert.NotNull(result.Error.Errors);
        Assert.True(result.Error.Errors.Values.ContainsKey("PageSize"));
    }

    // ── Helpers de Reflexión y Fakes ─────────────────────────────────────────

    private static List<Type> GetPaginatedQueryTypes()
    {
        var appAssembly = AssemblyType.Assembly;
        return appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.Name.EndsWith("Query", StringComparison.Ordinal) &&
                        t.GetProperties().Any(p => p.Name == "PageNumber" && p.PropertyType == typeof(int)) &&
                        t.GetProperties().Any(p => p.Name == "PageSize" && p.PropertyType == typeof(int)))
            .ToList();
    }

    private static object CreateQueryWithPaginationInstance(Type queryType, int pageNumber, int pageSize)
    {
        var ctor = queryType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = ctor.GetParameters().Select(p =>
        {
            if (p.Name == "PageNumber") return (object)pageNumber;
            if (p.Name == "PageSize") return (object)pageSize;
            if (p.ParameterType == typeof(Guid)) return Guid.NewGuid(); // Conforme a §8 de v7 (no Guid.Empty)
            if (p.ParameterType == typeof(Guid?)) return (Guid?)Guid.NewGuid();
            if (p.ParameterType == typeof(string)) return (string?)"test";
            if (p.ParameterType == typeof(bool?)) return (bool?)null;
            if (p.HasDefaultValue) return p.DefaultValue;
            return Activator.CreateInstance(p.ParameterType);
        }).ToArray();
        return ctor.Invoke(parameters);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? UserToReturn { get; set; }
        public int LastRequestedPageSize { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(UserToReturn);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(UserToReturn);

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Update(User user) { }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            LastRequestedPageSize = pageSize;
            IReadOnlyList<User> emptyList = Array.Empty<User>();
            return Task.FromResult((Items: emptyList, TotalCount: 0));
        }
    }

    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        private int _callCount;

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.CompletedTask;
        }

        public Task AddStockLevelAsync(StockLevel stockLevel, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.CompletedTask;
        }

        public Task UpdateStockLevelAsync(StockLevel stockLevel, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.CompletedTask;
        }

        public Task<StockLevel?> GetStockLevelAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult<StockLevel?>(null);
        }

        public Task<IReadOnlyList<StockLevel>> GetStockLevelsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult<IReadOnlyList<StockLevel>>(Array.Empty<StockLevel>());
        }

        public Task<decimal> ReconcileStockFromLedgerAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult(0m);
        }

        public Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
            Guid? productId,
            Guid? warehouseId,
            InventoryMovementType? movementType,
            DateTime? dateFrom,
            DateTime? dateTo,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            _callCount++;
            IReadOnlyList<InventoryMovement> emptyList = Array.Empty<InventoryMovement>();
            return Task.FromResult((Items: emptyList, TotalCount: 0));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private int _callCount;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult<Product?>(Product.Create("Product Test", Sku.Create("SKU123"), Money.Create(10.0m, "USD"), Guid.NewGuid()));
        }

        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult<Product?>(null);
        }

        public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult(false);
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.CompletedTask;
        }

        public void Update(Product product)
        {
            _callCount++;
        }

        public void Delete(Product product)
        {
            _callCount++;
        }

        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            Guid? categoryId = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            _callCount++;
            IReadOnlyList<Product> emptyList = Array.Empty<Product>();
            return Task.FromResult((Items: emptyList, TotalCount: 0));
        }
    }
}
