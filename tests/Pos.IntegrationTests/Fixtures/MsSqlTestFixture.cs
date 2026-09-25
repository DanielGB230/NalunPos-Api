using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.ExternalServices.Dummy;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Repositories;
using Testcontainers.MsSql;
using Xunit;

namespace Pos.IntegrationTests.Fixtures;

public class TestTenantContext : ICurrentTenantContext, ITenantSetter
{
    private Guid? _tenantId;
    public Guid? TenantId => _tenantId;
    public bool IsSuperAdmin { get; set; }
    public bool HasTenant => TenantId.HasValue;

    public TestTenantContext(Guid? tenantId, bool isSuperAdmin = false)
    {
        _tenantId = tenantId;
        IsSuperAdmin = isSuperAdmin;
    }

    public void SetTenantId(Guid? tenantId)
    {
        _tenantId = tenantId;
    }
}

public class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    public TestCurrentUserService(Guid? userId = null, string? email = null)
    {
        UserId = userId;
        UserEmail = email;
    }
}

public class DummyTestEventBus : IEventBus
{
    public Task PublishAsync(Pos.Application.IntegrationEvents.Contracts.IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class MsSqlTestFixture : IAsyncLifetime, IDisposable
{
    private readonly MsSqlContainer? _msSqlContainer;
    private string _connectionString = string.Empty;
    private bool _useContainer;

    public MsSqlTestFixture()
    {
        try
        {
            _msSqlContainer = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
        }
        catch
        {
            _msSqlContainer = null;
        }
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        if (_msSqlContainer != null)
        {
            try
            {
                await _msSqlContainer.StartAsync();
                _connectionString = _msSqlContainer.GetConnectionString();
                _useContainer = true;
            }
            catch
            {
                _useContainer = false;
            }
        }

        if (!_useContainer)
        {
            _connectionString = "Server=(localdb)\\mssqllocaldb;Database=NalunPos_IntegrationTestsDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
        }

        using var dbContext = CreateDbContext(tenantId: null, isSuperAdmin: true);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("==========================================================================");
        Console.WriteLine("✅ MS-SQL TEST FIXTURE INICIADO Y BASE CREADA:");
        Console.WriteLine($"   {ConnectionString}");
        Console.WriteLine("==========================================================================");
        Console.ResetColor();
    }

    public async Task DisposeAsync()
    {
        if (_useContainer && _msSqlContainer != null)
        {
            await _msSqlContainer.DisposeAsync();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public PosDbContext CreateDbContext(Guid? tenantId = null, bool? isSuperAdmin = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);

        bool superAdminFlag = isSuperAdmin ?? false;
        var tenantContext = new TestTenantContext(tenantId, superAdminFlag);
        var sessionContextInterceptor = new TenantSessionContextInterceptor(tenantContext);
        optionsBuilder.AddInterceptors(new TenantSaveChangesInterceptor(tenantContext), sessionContextInterceptor);

        return new PosDbContext(optionsBuilder.Options, sessionContextInterceptor: sessionContextInterceptor, currentTenantId: tenantId);
    }

    public IServiceProvider CreateServiceProvider(Guid? tenantId = null, bool? isSuperAdmin = null)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        bool superAdminFlag = isSuperAdmin ?? (!tenantId.HasValue);
        services.AddScoped<TestTenantContext>(_ => new TestTenantContext(tenantId, superAdminFlag));
        services.AddScoped<ICurrentTenantContext>(sp => sp.GetRequiredService<TestTenantContext>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<TestTenantContext>());

        var testUserService = new TestCurrentUserService();
        services.AddScoped<ICurrentUserService>(_ => testUserService);
        services.AddScoped<TestCurrentUserService>(_ => testUserService);

        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<TenantSessionContextInterceptor>();

        services.AddScoped<PosDbContext>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
            optionsBuilder.UseSqlServer(ConnectionString);

            var tenantInterceptor = sp.GetRequiredService<TenantSaveChangesInterceptor>();
            var sessionContextInterceptor = sp.GetRequiredService<TenantSessionContextInterceptor>();
            optionsBuilder.AddInterceptors(tenantInterceptor, sessionContextInterceptor);

            var tenantContext = sp.GetRequiredService<ICurrentTenantContext>();
            return new PosDbContext(optionsBuilder.Options, sessionContextInterceptor: sessionContextInterceptor, currentTenantId: tenantContext.TenantId);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PosDbContext>());

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPasswordHasher, Pos.Infrastructure.Authentication.PasswordHasher>();
        services.AddScoped<IAuthUserLookup, Pos.Infrastructure.Authentication.AuthUserLookup>();
        services.AddScoped<ICurrentUserPermissions, Pos.Infrastructure.Authentication.CurrentUserPermissions>();

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
        services.AddScoped<IAgentActionRecordRepository, AgentActionRecordRepository>();
        services.AddScoped<ISystemNotificationRepository, SystemNotificationRepository>();

        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();
        services.AddScoped<IEventBus, DummyTestEventBus>();
        services.AddTransient<IIntegrationEventHandler<DummyTenantIntegrationEvent>, DummyTenantIntegrationEventHandler>();

        services.AddMemoryCache();
#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        services.AddApplicationServices();

        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                var adminRole = db.Roles.FirstOrDefault(r => r.TenantId == tenantId.Value && r.Name == "TenantAdmin");
                if (adminRole == null)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Pos.Infrastructure.Persistence.Seed.DefaultRoleSeeder>>();
                    var seeder = new Pos.Infrastructure.Persistence.Seed.DefaultRoleSeeder(db, logger);
                    seeder.SeedRolesForTenantAsync(tenantId.Value).GetAwaiter().GetResult();
                    adminRole = db.Roles.First(r => r.TenantId == tenantId.Value && r.Name == "TenantAdmin");
                }

                var defaultEmail = new Email($"admin_{tenantId.Value.ToString()[..8]}@testtenant.com");
                var defaultUser = db.Users.FirstOrDefault(u => u.TenantId == tenantId.Value && u.Email == defaultEmail);
                if (defaultUser == null)
                {
                    defaultUser = User.Create(defaultEmail, new PasswordHash("hashedpass"), adminRole.Id, tenantId.Value, "Tenant", "Admin");
                    db.Users.Add(defaultUser);
                    db.SaveChangesAsync().GetAwaiter().GetResult();
                }

                testUserService.UserId = defaultUser.Id;
                testUserService.UserEmail = defaultUser.Email.Value;
            }
            else
            {
                // SuperAdmin Context
                var superAdminRole = db.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.Id == Role.SuperAdminRoleId);
                var permissions = new[]
                {
                    Permissions.Tenants.Create, Permissions.Tenants.View, Permissions.Tenants.Update, Permissions.Tenants.Delete,
                    Permissions.Users.Create, Permissions.Users.View, Permissions.Users.Update, Permissions.Users.Delete,
                    Permissions.Roles.Create, Permissions.Roles.View, Permissions.Roles.Update, Permissions.Roles.Delete
                };

                if (superAdminRole == null)
                {
                    var constructor = typeof(Role).GetConstructor(
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(Guid), typeof(Guid), typeof(string), typeof(string), typeof(IEnumerable<string>) },
                        null);

                    superAdminRole = (Role)constructor!.Invoke(new object[] { Role.SuperAdminRoleId, Guid.Empty, "SuperAdminRole", "Super Admin Role", permissions });
                    db.Roles.Add(superAdminRole);
                    db.SaveChangesAsync().GetAwaiter().GetResult();
                }
                else
                {
                    foreach (var p in permissions)
                    {
                        superAdminRole.AddPermission(p);
                    }
                    db.SaveChangesAsync().GetAwaiter().GetResult();
                }

                var superAdminEmail = new Email("superadmin_platform@test.com");
                var superAdminUser = db.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == superAdminEmail);
                if (superAdminUser == null)
                {
                    superAdminUser = User.Create(superAdminEmail, new PasswordHash("hashedpass"), Role.SuperAdminRoleId, null, "Super", "Admin");
                    db.Users.Add(superAdminUser);
                    db.SaveChangesAsync().GetAwaiter().GetResult();
                }

                testUserService.UserId = superAdminUser.Id;
                testUserService.UserEmail = superAdminUser.Email.Value;
            }
        }

        return sp;
    }
}
