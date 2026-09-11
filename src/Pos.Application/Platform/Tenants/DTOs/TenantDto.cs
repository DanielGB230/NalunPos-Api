namespace Pos.Application.Platform.Tenants.DTOs;

/// <summary>
/// DTO de transferencia de datos para la consulta de información de Tenants.
/// </summary>
public record TenantDto(
    Guid Id,
    string Name,
    string DocumentNumber,
    string Status,
    DateTime CreatedAtUtc
);
