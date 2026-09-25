namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato HTTP genérico para cambio de estado activo/inactivo (Soft-Delete / Activar).
/// </summary>
public record ChangeStatusRequest(bool IsActive);
