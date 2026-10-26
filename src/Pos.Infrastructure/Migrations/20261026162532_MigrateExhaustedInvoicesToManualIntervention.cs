using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateExhaustedInvoicesToManualIntervention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = 1;

INSERT INTO [dbo].[OutboxMessages] (
    [Id],
    [TenantId],
    [Type],
    [Content],
    [OccurredOnUtc],
    [ProcessedOnUtc],
    [RetryCount],
    [IsDeadLetter]
)
SELECT
    NEWID() AS [Id],
    i.[TenantId],
    'Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent' AS [Type],
    CONCAT(
        '{\""InvoiceId\"":\""', CAST(i.[Id] AS NVARCHAR(36)),
        '\"",\""SaleId\"":\""', CAST(i.[SaleId] AS NVARCHAR(36)),
        '\"",\""TenantId\"":\""', CAST(i.[TenantId] AS NVARCHAR(36)),
        '\"",\""DocumentNumber\"":\""', i.[DocumentNumber],
        '\"",\""Attempts\"":', CAST(i.[ReconciliationAttempts] AS NVARCHAR(10)),
        ',\""Reason\"":\""Superó el límite de reintentos de reconciliación fiscal sin confirmación del proveedor.\"",',
        '\""OccurredOnUtc\"":\""', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 127),
        'Z\""}'
    ) AS [Content],
    SYSUTCDATETIME() AS [OccurredOnUtc],
    NULL AS [ProcessedOnUtc],
    0 AS [RetryCount],
    0 AS [IsDeadLetter]
FROM [dbo].[Invoices] i
WHERE i.[Status] = 1 AND i.[ReconciliationAttempts] >= 5;

UPDATE [dbo].[Invoices]
SET [Status] = 5
WHERE [Status] = 1 AND [ReconciliationAttempts] >= 5;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
EXEC sys.sp_set_session_context @key = N'IsSuperAdmin', @value = 1;

DELETE FROM [dbo].[OutboxMessages]
WHERE [Type] = 'Pos.Domain.DomainEvents.InvoiceReconciliationExhaustedDomainEvent'
  AND [Content] LIKE '%Superó el límite de reintentos de reconciliación fiscal sin confirmación del proveedor.%';

UPDATE [dbo].[Invoices]
SET [Status] = 1
WHERE [Status] = 5 AND [ReconciliationAttempts] >= 5;
");
        }
    }
}
