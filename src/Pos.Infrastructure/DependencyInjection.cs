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
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<InsertOutboxMessagesInterceptor>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            var auditInterceptor = provider.GetRequiredService<AuditSaveChangesInterceptor>();
            var outboxInterceptor = provider.GetRequiredService<InsertOutboxMessagesInterceptor>();
            var tenantInterceptor = provider.GetRequiredService<TenantSaveChangesInterceptor>();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                // Fallback de desarrollo local si no se especifica cadena de conexión en appsettings
                options.UseInMemoryDatabase("NalunPosDb");
            }

            options.AddInterceptors(auditInterceptor, outboxInterceptor, tenantInterceptor);
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICashRegisterRepository, CashRegisterRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IPosDeviceRepository, PosDeviceRepository>();
        services.AddScoped<ISystemNotificationRepository, SystemNotificationRepository>();
        services.AddScoped<IAgentActionRecordRepository, AgentActionRecordRepository>();

        // Servicios de Seguridad y Contexto
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Event Bus y Notificaciones ACL
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        services.AddScoped<IPaymentGateway, DummyPaymentGateway>();
        services.AddScoped<IElectronicInvoicingService, DummyElectronicInvoicingService>();
        services.AddScoped<IPushNotificationService, DummyPushNotificationService>();
        services.AddScoped<IDemandForecastCapability, DummyDemandForecastAdapter>();

        return services;
    }
}
