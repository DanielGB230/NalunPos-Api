using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Multitenancy;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Provee el punto único y canónico para registrar la composición de interceptores de SaveChanges
/// de PosDbContext (TenantSaveChangesInterceptor, AuditSaveChangesInterceptor, InsertOutboxMessagesInterceptor).
/// </summary>
public static class PosDbContextInterceptorExtensions
{
    public static DbContextOptionsBuilder AddPosSaveChangesInterceptors(this DbContextOptionsBuilder options, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);

        return options.AddInterceptors(
            provider.GetRequiredService<TenantSaveChangesInterceptor>(),
            provider.GetRequiredService<AuditSaveChangesInterceptor>(),
            provider.GetRequiredService<InsertOutboxMessagesInterceptor>()
        );
    }
}
