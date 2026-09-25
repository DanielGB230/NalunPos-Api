using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'Security')
BEGIN
    EXEC('CREATE SCHEMA [Security]');
END;
");

            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION [Security].[fn_userTenantAccessPredicate](@TenantId UNIQUEIDENTIFIER)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS fn_userTenantAccessPredicate_result
WHERE
    (SESSION_CONTEXT(N'TenantId') IS NOT NULL AND @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER))
    OR (CAST(SESSION_CONTEXT(N'IsSuperAdmin') AS INT) = 1)
    OR (CAST(SESSION_CONTEXT(N'AllowGlobalUserLookup') AS INT) = 1)
    OR (SESSION_CONTEXT(N'TenantId') IS NULL AND (@TenantId IS NULL OR @TenantId = '00000000-0000-0000-0000-000000000000'));
");

            migrationBuilder.Sql(@"
CREATE OR ALTER FUNCTION [Security].[fn_tenantAccessPredicate](@TenantId UNIQUEIDENTIFIER)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS fn_tenantAccessPredicate_result
WHERE
    (SESSION_CONTEXT(N'TenantId') IS NOT NULL AND @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER))
    OR (CAST(SESSION_CONTEXT(N'IsSuperAdmin') AS INT) = 1)
    OR (SESSION_CONTEXT(N'TenantId') IS NULL AND (@TenantId IS NULL OR @TenantId = '00000000-0000-0000-0000-000000000000'));
");

            migrationBuilder.Sql(@"
CREATE SECURITY POLICY [Security].[TenantSecurityPolicy]
ADD FILTER PREDICATE [Security].[fn_userTenantAccessPredicate]([TenantId]) ON [dbo].[Users],
ADD BLOCK PREDICATE [Security].[fn_userTenantAccessPredicate]([TenantId]) ON [dbo].[Users],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Roles],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Roles],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Products],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Products],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Categories],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Categories],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Suppliers],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Suppliers],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[InventoryMovements],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[InventoryMovements],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Customers],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Customers],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[CashRegisters],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[CashRegisters],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[CashRegisterSessions],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[CashRegisterSessions],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Sales],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Sales],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PurchaseOrders],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PurchaseOrders],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Warehouses],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Warehouses],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Containers],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Containers],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockLevels],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockLevels],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockAdjustments],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockAdjustments],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockTransfers],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[StockTransfers],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Payments],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Payments],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Invoices],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Invoices],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[AuditLogs],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[AuditLogs],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Branches],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Branches],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PosDevices],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PosDevices],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[SystemNotifications],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[SystemNotifications],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[OutboxMessages],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[OutboxMessages],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[AgentActionRecords],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[AgentActionRecords]
WITH (STATE = ON);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP SECURITY POLICY IF EXISTS [Security].[TenantSecurityPolicy];
DROP FUNCTION IF EXISTS [Security].[fn_userTenantAccessPredicate];
DROP FUNCTION IF EXISTS [Security].[fn_tenantAccessPredicate];
DROP SCHEMA IF EXISTS [Security];
");
        }
    }
}
