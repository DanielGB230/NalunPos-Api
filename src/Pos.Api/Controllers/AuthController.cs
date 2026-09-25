using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers;

/// <summary>
/// Controlador de Autenticación del Sistema POS SaaS.
/// Ultra-delgado: Delega el 100% de la ejecución a IDispatcher.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AuthController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Autentica a un usuario (SuperAdmin o miembro de Tenant) con sus credenciales.
    /// </summary>
    /// <param name="command">Command de inicio de sesión (email y contraseña)</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Respuesta con Token JWT y datos de perfil</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] Pos.Api.Contracts.Requests.LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
