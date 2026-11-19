using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Authentication.Commands.Login;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.RefreshToken;

/// <summary>
/// Command para rotar el refresh token (<c>[AllowAnonymous]</c> en el controlador).
/// El token claro viaja en la cookie HttpOnly <c>nalunpos_rt</c>; el controller lo lee
/// y lo inyecta en el command.
/// </summary>
[AllowsNoTenant]
[PublicUseCase]
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<LoginResponse>>;
