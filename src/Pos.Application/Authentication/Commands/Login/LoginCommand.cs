using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// Command para solicitar la autenticación de un usuario con credenciales (email + password).
/// </summary>
[PublicUseCase]
public record LoginCommand(
    string Email,
    string Password
) : ICommand<Result<LoginResponse>>;
