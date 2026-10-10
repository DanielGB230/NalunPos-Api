namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Authorization;

public sealed class FakeCurrentUserPermissions : ICurrentUserPermissions
{
    public HashSet<string> PermissionsSet { get; } =
    [
        Permissions.Users.View,
        Permissions.Inventory.View,
        Permissions.Products.View,
        Permissions.Categories.View,
        Permissions.Sales.View,
        Permissions.Payments.View,
        Permissions.Customers.View,
        Permissions.CashRegisters.View,
        Permissions.Branches.View,
        Permissions.Warehouses.View,
        Permissions.Suppliers.View,
        Permissions.PurchaseOrders.View,
        Permissions.StockTransfers.View,
        Permissions.StockAdjustments.View,
        Permissions.Invoices.View,
        Permissions.PosDevices.View,
        Permissions.Tenants.View,
        Permissions.Roles.View,
        Permissions.Notifications.View,
        Permissions.AiGovernance.View
    ];

    public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlySet<string>>(PermissionsSet);
    }

    public Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PermissionsSet.Contains(permission));
    }
}
