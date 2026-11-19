using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.Logout;

/// <summary>
/// Command para cerrar sesión (<c>[AllowAnonymous]</c> en el controlador).
/// Idempotente: si no hay cookie o el token ya está revocado, aún retorna 204.
/// </summary>
[AllowsNoTenant]
[PublicUseCase]
public sealed record LogoutCommand(string? RefreshToken) : ICommand<Result>;
