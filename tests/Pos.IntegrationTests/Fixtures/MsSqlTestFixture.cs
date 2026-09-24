using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.ExternalServices.Dummy;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
using Xunit;

namespace Pos.IntegrationTests.Fixtures;

public class TestTenantContext : ICurrentTenantContext
{
    public Guid? TenantId { get; }
    public bool IsSuperAdmin => false;
    public bool HasTenant => TenantId.HasValue;

    public TestTenantContext(Guid? tenantId)
    {
        TenantId = tenantId;
    }
}

public class MsSqlTestFixture : IAsyncLifetime, IDisposable
{
    private readonly MsSqlContainer _msSqlContainer;

    public MsSqlTestFixture()
    {
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public string ConnectionString => _msSqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("✅ TESTCONTAINERS MS-SQL INICIADO Y BASE CREADA:");
        Console.WriteLine($"   {ConnectionString}");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }

    public async Task DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public PosDbContext CreateDbContext(Guid? tenantId = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);

        var tenantContext = new TestTenantContext(tenantId);
        optionsBuilder.AddInterceptors(new TenantSaveChangesInterceptor(tenantContext));

        return new PosDbContext(optionsBuilder.Options, currentTenantId: tenantId);
    }

    public IServiceProvider CreateServiceProvider(Guid? tenantId = null)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddScoped<ICurrentTenantContext>(_ => new TestTenantContext(tenantId));
        services.AddScoped<TenantSaveChangesInterceptor>();

        services.AddScoped<PosDbContext>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
            optionsBuilder.UseSqlServer(ConnectionString);

            var tenantInterceptor = sp.GetRequiredService<TenantSaveChangesInterceptor>();
            optionsBuilder.AddInterceptors(tenantInterceptor);

            return new PosDbContext(optionsBuilder.Options, currentTenantId: tenantId);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PosDbContext>());

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, Pos.Infrastructure.Authentication.PasswordHasher>();
        services.AddScoped<IAuthUserLookup, Pos.Infrastructure.Authentication.AuthUserLookup>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockLevelRepository, StockLevelRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();

        services.AddApplicationServices();

        return services.BuildServiceProvider();
    }
}
