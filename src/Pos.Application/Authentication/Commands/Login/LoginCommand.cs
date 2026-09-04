using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// Command para solicitar la autenticación de un usuario con credenciales (email + password).
/// </summary>
public record LoginCommand(
    string Email,
    string Password
) : ICommand<Result<LoginResponse>>;
