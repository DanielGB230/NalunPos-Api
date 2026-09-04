using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Interceptors;
using Pos.Infrastructure.Persistence.Outbox;

namespace Pos.Infrastructure.Persistence.Context;

public class ApplicationDbContext : DbContext, IUnitOfWork
{
    private readonly AuditSaveChangesInterceptor? _auditInterceptor;
    private readonly InsertOutboxMessagesInterceptor? _outboxInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditSaveChangesInterceptor? auditInterceptor = null,
        InsertOutboxMessagesInterceptor? outboxInterceptor = null) : base(options)
    {
        _auditInterceptor = auditInterceptor;
        _outboxInterceptor = outboxInterceptor;
    }

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
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();
    public DbSet<SystemNotification> SystemNotifications => Set<SystemNotification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AgentActionRecord> AgentActionRecords => Set<AgentActionRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (_auditInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditInterceptor);
        }
        if (_outboxInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_outboxInterceptor);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica automáticamente todas las configuraciones IEntityTypeConfiguration del ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
