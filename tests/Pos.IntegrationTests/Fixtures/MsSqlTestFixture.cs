// Integration tests corren contra SQL Server Express local con base dedicada (NalunPosDb_IntegrationTests). Migrar a Testcontainers cuando se configure un pipeline de CI/CD real (disparador objetivo, no antes).

using Microsoft.Data.SqlClient;
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
    private string? _connectionString;

    public string ConnectionString
    {
        get
        {
            if (_connectionString != null) return _connectionString;
            _connectionString = ResolveConnectionString();
            return _connectionString;
        }
    }

    private static string ResolveConnectionString()
    {
        string? envConn = Environment.GetEnvironmentVariable("IntegrationTestConnectionString");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            return envConn;
        }

        string[] candidateConnectionStrings = new[]
        {
            "Server=.\\SQLEXPRESS;Database=NalunPosDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;",
            "Server=DANI\\SQLEXPRESS;Database=NalunPosDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;",
            "Server=(localdb)\\mssqllocaldb;Database=NalunPosDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;",
            "Server=localhost;Database=NalunPosDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
        };

        foreach (var cs in candidateConnectionStrings)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" };
                using var conn = new SqlConnection(builder.ConnectionString);
                conn.Open();
                return cs;
            }
            catch
            {
                // Probar el siguiente servidor local candidate
            }
        }

        return candidateConnectionStrings[0];
    }

    public async Task InitializeAsync()
    {
        using var dbContext = CreateDbContext();
        
        // Recreación atómica y limpia de la base de datos dedicada NalunPosDb_IntegrationTests
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("✅ SQL SERVER EXPRESS LOCAL DEDICADO CONECTADO Y BASE RECREADA:");
        Console.WriteLine($"   {ConnectionString}");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
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

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
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
