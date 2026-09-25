namespace Pos.Application.Common.Authorization;

public static class Permissions
{
    public static class Products
    {
        public const string View = "Permissions.Products.View";
        public const string Create = "Permissions.Products.Create";
        public const string Update = "Permissions.Products.Update";
        public const string Delete = "Permissions.Products.Delete";
    }

    public static class Categories
    {
        public const string View = "Permissions.Categories.View";
        public const string Create = "Permissions.Categories.Create";
        public const string Update = "Permissions.Categories.Update";
        public const string Delete = "Permissions.Categories.Delete";
    }

    public static class Sales
    {
        public const string View = "Permissions.Sales.View";
        public const string Create = "Permissions.Sales.Create";
        public const string Update = "Permissions.Sales.Update";
        public const string Delete = "Permissions.Sales.Delete";
    }

    public static class Payments
    {
        public const string View = "Permissions.Payments.View";
        public const string Process = "Permissions.Payments.Process";
    }

    public static class Customers
    {
        public const string View = "Permissions.Customers.View";
        public const string Create = "Permissions.Customers.Create";
        public const string Update = "Permissions.Customers.Update";
        public const string Delete = "Permissions.Customers.Delete";
    }

    public static class CashRegisters
    {
        public const string View = "Permissions.CashRegisters.View";
        public const string Create = "Permissions.CashRegisters.Create";
        public const string OpenSession = "Permissions.CashRegisters.OpenSession";
        public const string CloseSession = "Permissions.CashRegisters.CloseSession";
    }

    public static class Branches
    {
        public const string View = "Permissions.Branches.View";
        public const string Create = "Permissions.Branches.Create";
        public const string Update = "Permissions.Branches.Update";
        public const string Delete = "Permissions.Branches.Delete";
    }

    public static class Warehouses
    {
        public const string View = "Permissions.Warehouses.View";
        public const string Create = "Permissions.Warehouses.Create";
        public const string Update = "Permissions.Warehouses.Update";
        public const string Delete = "Permissions.Warehouses.Delete";
    }

    public static class Suppliers
    {
        public const string View = "Permissions.Suppliers.View";
        public const string Create = "Permissions.Suppliers.Create";
        public const string Update = "Permissions.Suppliers.Update";
        public const string Delete = "Permissions.Suppliers.Delete";
    }

    public static class PurchaseOrders
    {
        public const string View = "Permissions.PurchaseOrders.View";
        public const string Create = "Permissions.PurchaseOrders.Create";
        public const string Send = "Permissions.PurchaseOrders.Send";
        public const string Receive = "Permissions.PurchaseOrders.Receive";
    }

    public static class StockTransfers
    {
        public const string View = "Permissions.StockTransfers.View";
        public const string Create = "Permissions.StockTransfers.Create";
    }

    public static class StockAdjustments
    {
        public const string View = "Permissions.StockAdjustments.View";
        public const string Create = "Permissions.StockAdjustments.Create";
    }

    public static class Inventory
    {
        public const string View = "Permissions.Inventory.View";
        public const string AdjustStock = "Permissions.Inventory.AdjustStock";
        public const string TransferStock = "Permissions.Inventory.TransferStock";
    }

    public static class Invoices
    {
        public const string View = "Permissions.Invoices.View";
        public const string Issue = "Permissions.Invoices.Issue";
    }

    public static class PosDevices
    {
        public const string View = "Permissions.PosDevices.View";
        public const string Register = "Permissions.PosDevices.Register";
        public const string Ping = "Permissions.PosDevices.Ping";
    }

    public static class Tenants
    {
        public const string View = "Permissions.Tenants.View";
        public const string Create = "Permissions.Tenants.Create";
        public const string Update = "Permissions.Tenants.Update";
        public const string Delete = "Permissions.Tenants.Delete";
    }

    public static class Roles
    {
        public const string View = "Permissions.Roles.View";
        public const string Create = "Permissions.Roles.Create";
        public const string Update = "Permissions.Roles.Update";
        public const string Delete = "Permissions.Roles.Delete";
    }

    public static class Users
    {
        public const string View = "Permissions.Users.View";
        public const string Create = "Permissions.Users.Create";
        public const string Update = "Permissions.Users.Update";
        public const string Delete = "Permissions.Users.Delete";
    }

    public static class Notifications
    {
        public const string View = "Permissions.Notifications.View";
        public const string Create = "Permissions.Notifications.Create";
        public const string MarkRead = "Permissions.Notifications.MarkRead";
    }

    public static class AiGovernance
    {
        public const string View = "Permissions.AiGovernance.View";
        public const string ProposeAction = "Permissions.AiGovernance.ProposeAction";
        public const string ReviewAction = "Permissions.AiGovernance.ReviewAction";
    }
}
