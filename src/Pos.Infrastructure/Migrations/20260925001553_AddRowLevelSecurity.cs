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
CREATE OR ALTER FUNCTION [Security].[fn_tenantAccessPredicate](@TenantId UNIQUEIDENTIFIER)
RETURNS TABLE
WITH SCHEMABINDING
AS
RETURN SELECT 1 AS fn_tenantAccessPredicate_result
WHERE
    (SESSION_CONTEXT(N'TenantId') IS NOT NULL AND @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER))
    OR (CAST(SESSION_CONTEXT(N'IsSuperAdmin') AS INT) = 1)
    OR (CAST(SESSION_CONTEXT(N'AllowGlobalUserLookup') AS INT) = 1)
    OR (SESSION_CONTEXT(N'TenantId') IS NULL AND (@TenantId IS NULL OR @TenantId = '00000000-0000-0000-0000-000000000000'));
");

            migrationBuilder.Sql(@"
CREATE SECURITY POLICY [Security].[TenantSecurityPolicy]
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Users],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Users],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Roles],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Roles],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Products],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Products],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Categories],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Categories],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Customers],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Customers],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Sales],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Sales],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Warehouses],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[Warehouses],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[InventoryMovements],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[InventoryMovements],
ADD FILTER PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PurchaseOrders],
ADD BLOCK PREDICATE [Security].[fn_tenantAccessPredicate]([TenantId]) ON [dbo].[PurchaseOrders]
WITH (STATE = ON);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP SECURITY POLICY IF EXISTS [Security].[TenantSecurityPolicy];
DROP FUNCTION IF EXISTS [Security].[fn_tenantAccessPredicate];
DROP SCHEMA IF EXISTS [Security];
");
        }
    }
}
