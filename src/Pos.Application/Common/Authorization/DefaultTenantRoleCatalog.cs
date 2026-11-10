using Pos.Domain.Common;

namespace Pos.Application.Common.Authorization;

/// <summary>
/// Catálogo canónico y fuente única de verdad de los 4 roles de sistema de cada tenant.
/// Consumido por el aprovisionamiento de tenants y por el seeder idempotente.
/// </summary>
public static class DefaultTenantRoleCatalog
{
    public static IReadOnlyList<TenantRoleDefinition> All { get; } =
    [
        new(
            TenantRoles.TenantAdmin,
            "Administrador del Tenant",
            [
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
            ]),
        new(
            TenantRoles.Supervisor,
            "Supervisor de Operaciones y Ventas",
            [
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
            ]),
        new(
            TenantRoles.InventoryManager,
            "Encargado de Inventario y Almacenes",
            [
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
            ]),
        new(
            TenantRoles.Cajero,
            "Cajero Operador de Turno",
            [
                Permissions.CashRegisters.View, Permissions.CashRegisters.OpenSession, Permissions.CashRegisters.CloseSession,
                Permissions.Categories.View,
                Permissions.Products.View,
                Permissions.Customers.View, Permissions.Customers.Create, Permissions.Customers.Update,
                Permissions.Sales.View, Permissions.Sales.Create,
                Permissions.Payments.View, Permissions.Payments.Process,
                Permissions.Invoices.View, Permissions.Invoices.Issue,
                Permissions.Notifications.View, Permissions.Notifications.MarkRead
            ])
    ];
}

