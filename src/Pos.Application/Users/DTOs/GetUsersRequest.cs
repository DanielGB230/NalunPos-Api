using Pos.Application.Common.Models;

namespace Pos.Application.Users.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de usuarios.
/// Consolida el filtro de estado en bool? IsActive.
/// </summary>
public record GetUsersRequest(
    string? SearchTerm = null,
    bool? IsActive = null
) : PaginationRequest;
