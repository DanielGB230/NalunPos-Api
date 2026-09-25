using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Authorization;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Seed;

public class DefaultRoleSeeder
{
    private readonly PosDbContext _context;
    private readonly ILogger<DefaultRoleSeeder> _logger;

    public DefaultRoleSeeder(PosDbContext context, ILogger<DefaultRoleSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedRolesForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // 1. TenantAdmin
        await SeedRoleAsync(tenantId, "TenantAdmin", "Administrador del Tenant", new[]
        {
            Permissions.Users.View, Permissions.Users.Create, Permissions.Users.Update, Permissions.Users.Delete,
            Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.Update, Permissions.Roles.Delete,
            Permissions.Products.View, Permissions.Products.Create, Permissions.Products.Update, Permissions.Products.Delete,
            Permissions.Categories.View, Permissions.Categories.Create, Permissions.Categories.Update, Permissions.Categories.Delete,
            Permissions.Sales.View, Permissions.Sales.Create, Permissions.Sales.Update, Permissions.Sales.Delete,
            Permissions.Payments.View, Permissions.Payments.Process,
            Permissions.Customers.View, Permissions.Customers.Create, Permissions.Customers.Update, Permissions.Customers.Delete,
            Permissions.CashRegisters.View, Permissions.CashRegisters.Create, Permissions.CashRegisters.OpenSession, Permissions.CashRegisters.CloseSession,
            Permissions.Branches.View, Permissions.Branches.Create, Permissions.Branches.Update, Permissions.Branches.Delete,
            Permissions.Warehouses.View, Permissions.Warehouses.Create, Permissions.Warehouses.Update, Permissions.Warehouses.Delete,
            Permissions.Suppliers.View, Permissions.Suppliers.Create, Permissions.Suppliers.Update, Permissions.Suppliers.Delete,
            Permissions.PurchaseOrders.View, Permissions.PurchaseOrders.Create, Permissions.PurchaseOrders.Send, Permissions.PurchaseOrders.Receive,
            Permissions.StockTransfers.View, Permissions.StockTransfers.Create,
            Permissions.StockAdjustments.View, Permissions.StockAdjustments.Create,
            Permissions.Inventory.View, Permissions.Inventory.AdjustStock, Permissions.Inventory.TransferStock,
            Permissions.Invoices.View, Permissions.Invoices.Issue,
            Permissions.PosDevices.View, Permissions.PosDevices.Register, Permissions.PosDevices.Ping,
            Permissions.Tenants.View, Permissions.Tenants.Update,
            Permissions.Notifications.View, Permissions.Notifications.Create, Permissions.Notifications.MarkRead,
            Permissions.AiGovernance.View, Permissions.AiGovernance.ProposeAction, Permissions.AiGovernance.ReviewAction
        }, cancellationToken);

        // 2. Supervisor
        await SeedRoleAsync(tenantId, "Supervisor", "Supervisor de Operaciones y Ventas", new[]
        {
            Permissions.CashRegisters.View, Permissions.CashRegisters.OpenSession, Permissions.CashRegisters.CloseSession,
            Permissions.Categories.View, Permissions.Categories.Create, Permissions.Categories.Update,
            Permissions.Products.View, Permissions.Products.Create, Permissions.Products.Update,
            Permissions.Customers.View, Permissions.Customers.Create, Permissions.Customers.Update,
            Permissions.Sales.View, Permissions.Sales.Create, Permissions.Sales.Update, Permissions.Sales.Delete,
            Permissions.Payments.View, Permissions.Payments.Process,
            Permissions.Invoices.View, Permissions.Invoices.Issue,
            Permissions.Inventory.View, Permissions.Inventory.AdjustStock, Permissions.Inventory.TransferStock,
            Permissions.StockAdjustments.View, Permissions.StockAdjustments.Create,
            Permissions.StockTransfers.View, Permissions.StockTransfers.Create,
            Permissions.Warehouses.View,
            Permissions.Suppliers.View,
            Permissions.PurchaseOrders.View, Permissions.PurchaseOrders.Create, Permissions.PurchaseOrders.Send, Permissions.PurchaseOrders.Receive,
            Permissions.Notifications.View, Permissions.Notifications.MarkRead,
            Permissions.AiGovernance.View
        }, cancellationToken);

        // 3. InventoryManager
        await SeedRoleAsync(tenantId, "InventoryManager", "Encargado de Inventario y Almacenes", new[]
        {
            Permissions.Products.View, Permissions.Products.Create, Permissions.Products.Update, Permissions.Products.Delete,
            Permissions.Categories.View, Permissions.Categories.Create, Permissions.Categories.Update, Permissions.Categories.Delete,
            Permissions.Warehouses.View, Permissions.Warehouses.Create, Permissions.Warehouses.Update,
            Permissions.Suppliers.View, Permissions.Suppliers.Create, Permissions.Suppliers.Update, Permissions.Suppliers.Delete,
            Permissions.PurchaseOrders.View, Permissions.PurchaseOrders.Create, Permissions.PurchaseOrders.Send, Permissions.PurchaseOrders.Receive,
            Permissions.StockTransfers.View, Permissions.StockTransfers.Create,
            Permissions.StockAdjustments.View, Permissions.StockAdjustments.Create,
            Permissions.Inventory.View, Permissions.Inventory.AdjustStock, Permissions.Inventory.TransferStock,
            Permissions.PosDevices.View,
            Permissions.Notifications.View, Permissions.Notifications.MarkRead
        }, cancellationToken);

        // 4. Cajero
        await SeedRoleAsync(tenantId, "Cajero", "Cajero Operador de Turno", new[]
        {
            Permissions.CashRegisters.View, Permissions.CashRegisters.OpenSession, Permissions.CashRegisters.CloseSession,
            Permissions.Categories.View,
            Permissions.Products.View,
            Permissions.Customers.View, Permissions.Customers.Create, Permissions.Customers.Update,
            Permissions.Sales.View, Permissions.Sales.Create,
            Permissions.Payments.View, Permissions.Payments.Process,
            Permissions.Invoices.View, Permissions.Invoices.Issue,
            Permissions.Notifications.View, Permissions.Notifications.MarkRead
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleAsync(
        Guid tenantId,
        string name,
        string description,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name, cancellationToken);
        if (role == null)
        {
            role = Role.Create(tenantId, name, description);
            foreach (var perm in permissions)
            {
                role.AddPermission(perm);
            }
            _context.Roles.Add(role);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Creado rol {RoleName} para el tenant {TenantId} con {Count} permisos.", name, tenantId, permissions.Count());
            }
        }
        else
        {
            // Garantizar idempotencia asegurando que existan los permisos especificados
            foreach (var perm in permissions)
            {
                if (!role.Permissions.Contains(perm))
                {
                    role.AddPermission(perm);
                }
            }
        }
    }
}
