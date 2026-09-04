using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Infrastructure.Persistence.Context;

/// <summary>
/// DbContext principal y canónico de la solución NalunPOS.
///
/// Responsabilidades:
///   1. Registro de entidades del dominio (DbSets).
///   2. Aplicación de configuraciones Fluent API por ensamblado.
///   3. Global Query Filter multi-tenant dinámico, idiomatic EF Core:
///      Se captura el <c>Guid? _currentTenantId</c> en el constructor desde
///      <c>ICurrentTenantContext</c>, y el filtro lo referencia como campo de
///      instancia. EF Core evalúa este campo en cada query (no en build-time),
///      garantizando aislamiento correcto por request sin expression trees manuales.
///   4. Inyección de interceptores de auditoría y outbox vía <c>OnConfiguring</c>.
/// </summary>
public class PosDbContext : DbContext, IUnitOfWork
{
    // ── Interceptores (Scoped, resueltos por request) ─────────────────────────
    private readonly AuditSaveChangesInterceptor? _auditInterceptor;
    private readonly InsertOutboxMessagesInterceptor? _outboxInterceptor;

    // ── Estado multi-tenant capturado en construcción ─────────────────────────
    /// <summary>
    /// TenantId activo en el request actual.
    /// <c>null</c> indica que el contexto no tiene restricción de tenant
    /// (SuperAdmin, seeder, design-time, background worker).
    /// </summary>
    private readonly Guid? _currentTenantId;

    // ── Constructor ───────────────────────────────────────────────────────────
    public PosDbContext(
        DbContextOptions<PosDbContext> options,
        AuditSaveChangesInterceptor? auditInterceptor = null,
        InsertOutboxMessagesInterceptor? outboxInterceptor = null,
        Guid? currentTenantId = null) : base(options)
    {
        _auditInterceptor = auditInterceptor;
        _outboxInterceptor = outboxInterceptor;
        _currentTenantId = currentTenantId;
    }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();
    public DbSet<SystemNotification> SystemNotifications => Set<SystemNotification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AgentActionRecord> AgentActionRecords => Set<AgentActionRecord>();

    // ── Configuración EF Core ─────────────────────────────────────────────────

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (_auditInterceptor is not null)
            optionsBuilder.AddInterceptors(_auditInterceptor);

        if (_outboxInterceptor is not null)
            optionsBuilder.AddInterceptors(_outboxInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las IEntityTypeConfiguration<T> del ensamblado Infrastructure
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);

        // Global Query Filters multi-tenant (ver ApplyTenantQueryFilters)
        ApplyTenantQueryFilters(modelBuilder);
    }

    // ── Global Query Filter Multi-Tenant ─────────────────────────────────────

    /// <summary>
    /// Registra un Global Query Filter en cada entidad que implementa
    /// <see cref="ITenantOwnedEntity"/>.
    ///
    /// Patrón idiomático de EF Core (sin Expression Trees manuales):
    /// La lambda captura <c>this._currentTenantId</c> como closure.
    /// EF Core lee el valor del campo en cada ejecución de query,
    /// no en tiempo de construcción del modelo.
    ///
    /// Semántica:
    ///   - <c>_currentTenantId == null</c> → acceso irrestricto
    ///     (SuperAdmin, seeder, background worker, design-time CLI).
    ///   - <c>_currentTenantId != null</c> → filtra por tenant activo.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            if (entityType.ClrType == null || !typeof(ITenantOwnedEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Usamos SetQueryFilter con el tipo genérico vía reflexión de tipos,
            // pero la lambda en sí es type-safe y no usa string de nombres de campo.
            var method = typeof(PosDbContext)
                .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>
    /// Registra el filtro de tenant fuertemente tipado para <typeparamref name="TEntity"/>.
    /// La lambda es compilada por C# (no por Expression Trees manuales) → type-safe y refactor-safe.
    /// </summary>
    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwnedEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _currentTenantId == null || e.TenantId == _currentTenantId.Value);
    }
}
