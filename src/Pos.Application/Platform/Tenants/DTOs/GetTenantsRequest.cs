using Pos.Application.Common.Models;

namespace Pos.Application.Platform.Tenants.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de Tenants (Platform).
/// </summary>
public record GetTenantsRequest(
    string? SearchTerm = null
) : PaginationRequest;
