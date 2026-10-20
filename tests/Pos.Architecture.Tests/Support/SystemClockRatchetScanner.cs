using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pos.Architecture.Tests.Support;

public sealed record SystemClockFinding(
    string FilePath,
    string MemberAccess,
    int LineNumber,
    string LineSnippet);

public static class SystemClockRatchetScanner
{
    /// <summary>
    /// Lista ratchet explícita de archivos de entidad en Pos.Domain que tienen pendiente su migración
    /// a TimeProvider (se migrarán en los pasos 7.2, 7.3 y 7.4).
    /// </summary>
    public static readonly HashSet<string> AllowedDomainEntitiesRatchet = new(StringComparer.OrdinalIgnoreCase)
    {
        "AgentActionRecord.cs",
        "AuditLog.cs",
        "Branch.cs",
        "CashRegister.cs",
        "CashRegisterSession.cs",
        "Category.cs",
        "Container.cs",
        "Customer.cs",
        "InventoryMovement.cs",
        "Invoice.cs",
        "Payment.cs",
        "PosDevice.cs",
        "Product.cs",
        "PurchaseOrder.cs",
        "Role.cs",
        "Sale.cs",
        "StockAdjustment.cs",
        "StockTransfer.cs",
        "Supplier.cs",
        "SystemNotification.cs",
        "Tenant.cs",
        "User.cs",
        "Warehouse.cs"
    };

    public static IReadOnlyList<SystemClockFinding> Scan(SyntaxNode root, string filePath = "")
    {
        var findings = new List<SystemClockFinding>();

        var memberAccessExpressions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccessExpressions)
        {
            var expressionText = memberAccess.Expression.ToString();
            var nameText = memberAccess.Name.Identifier.ValueText;

            bool isProhibitedAccess = false;

            if ((expressionText == "DateTime" || expressionText == "System.DateTime") &&
                (nameText == "UtcNow" || nameText == "Now"))
            {
                isProhibitedAccess = true;
            }
            else if ((expressionText == "DateTimeOffset" || expressionText == "System.DateTimeOffset") &&
                     (nameText == "UtcNow" || nameText == "Now"))
            {
                isProhibitedAccess = true;
            }

            if (isProhibitedAccess)
            {
                var lineSpan = memberAccess.GetLocation().GetLineSpan();
                int lineNumber = lineSpan.StartLinePosition.Line + 1;
                string snippet = memberAccess.ToString();

                findings.Add(new SystemClockFinding(filePath, $"{expressionText}.{nameText}", lineNumber, snippet));
            }
        }

        return findings;
    }
}
