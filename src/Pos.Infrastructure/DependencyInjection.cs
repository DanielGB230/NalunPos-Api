using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.AI.Abstractions;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.AiOrchestration.Adapters;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.EventBus.RabbitMq;
using Pos.Infrastructure.ExternalServices.Dummy;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Notifications.Dummy;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Persistence.Seed;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        // ── HTTP / Multitenancy ───────────────────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();

        // ── Interceptores EF Core (Scoped: un ciclo de vida por request) ─────
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<InsertOutboxMessagesInterceptor>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        // ── PosDbContext ──────────────────────────────────────────────────────
        // El overload (IServiceProvider, DbContextOptionsBuilder) permite resolver
        // servicios Scoped en el factory y construir el contexto correctamente.
        //
        // Diseño deliberado: el TenantId se extrae AQUÍ (como Guid?), no dentro
        // del DbContext. Esto desacopla PosDbContext de ICurrentTenantContext
        // (y por ende de IHttpContextAccessor), mejorando la testabilidad.
        services.AddDbContext<PosDbContext>((provider, options) =>
        {
            var auditInterceptor = provider.GetRequiredService<AuditSaveChangesInterceptor>();
            var outboxInterceptor = provider.GetRequiredService<InsertOutboxMessagesInterceptor>();
            var tenantInterceptor = provider.GetRequiredService<TenantSaveChangesInterceptor>();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString,
                    sql => sql
                        .MigrationsAssembly(typeof(PosDbContext).Assembly.FullName)
                        .CommandTimeout(30)
                        .EnableRetryOnFailure(maxRetryCount: 3));
            }
            else
            {
                // InMemory para tests de integración (nunca en producción)
                options.UseInMemoryDatabase("NalunPosDb_Test");
            }

            options.AddInterceptors(auditInterceptor, outboxInterceptor, tenantInterceptor);
        });

        // Factory override para inyectar el currentTenantId en cada instancia de PosDbContext.
        // Se sobreescribe el registro de AddDbContext para que el proveedor resuelva
        // ICurrentTenantContext y lo convierta a Guid? antes de construir el contexto.
        services.AddScoped<PosDbContext>(provider =>
        {
            var options = provider.GetRequiredService<DbContextOptions<PosDbContext>>();
            var auditInterceptor = provider.GetRequiredService<AuditSaveChangesInterceptor>();
            var outboxInterceptor = provider.GetRequiredService<InsertOutboxMessagesInterceptor>();
            var tenantInterceptor = provider.GetRequiredService<TenantSaveChangesInterceptor>();
            var tenantContext = provider.GetService<ICurrentTenantContext>();

            // Extracción del TenantId: desacopla PosDbContext de la infraestructura HTTP
            Guid? currentTenantId = tenantContext?.TenantId;

            return new PosDbContext(options, auditInterceptor, outboxInterceptor, tenantInterceptor, currentTenantId);
        });

        // ── Repositorios y UnitOfWork ─────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockLevelRepository, StockLevelRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IPosDeviceRepository, PosDeviceRepository>();
        services.AddScoped<ISystemNotificationRepository, SystemNotificationRepository>();
        services.AddScoped<IAgentActionRecordRepository, AgentActionRecordRepository>();

        // ── Seguridad y Autenticación ─────────────────────────────────────────
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthUserLookup, AuthUserLookup>();

        // ── Seeders ───────────────────────────────────────────────────────────
        services.AddScoped<SuperAdminSeeder>();

        // ── Event Bus y ACL de servicios externos ─────────────────────────────
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();
        services.AddScoped<IElectronicInvoicingService, DummyElectronicInvoicingService>();
        services.AddScoped<IPushNotificationService, DummyPushNotificationService>();
        services.AddScoped<IDemandForecastCapability, DummyDemandForecastAdapter>();

        return services;
    }
}
