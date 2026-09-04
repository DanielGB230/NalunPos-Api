namespace Pos.Infrastructure.Observability;

/// <summary>
/// Provee el contexto de correlación para observabilidad (CorrelationId, TenantId, UserId)
/// para adjuntar a cada log estructurado y traza distribuida.
/// </summary>
public class CorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
