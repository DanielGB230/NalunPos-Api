using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;

namespace Pos.Application.Authentication.Queries.GetCurrentUser;

/// <summary>
/// Query para obtener la identidad del usuario autenticado desde los claims del JWT.
/// <c>[Authorize]</c> se resuelve en el controlador; el query es <c>[PublicUseCase]</c>
/// para que el behavior de autorización no falle cerrado por falta de token
/// (el middleware de autenticación ya rechazó a los no autenticados).
/// </summary>
[AllowsNoTenant]
[PublicUseCase]
public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;
