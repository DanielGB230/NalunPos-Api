using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pos.Api.Extensions;
using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Authentication.Commands.Logout;
using Pos.Application.Authentication.Commands.RefreshToken;
using Pos.Application.Authentication.Queries.GetCurrentUser;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Options;

namespace Pos.Api.Controllers;

/// <summary>
/// Controlador de Autenticación del Sistema POS SaaS.
/// Ultra-delgado: Delega el 100% de la ejecución a IDispatcher.
/// No accede a <c>result.Value</c> directamente (usa extensiones en <c>Pos.Api/Extensions</c>).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces(System.Net.Mime.MediaTypeNames.Application.Json)]
[EnableRateLimiting("GlobalApiPolicy")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly RefreshTokenCookieWriter _cookieWriter;
    private readonly string _cookieName;

    public AuthController(
        IDispatcher dispatcher,
        RefreshTokenCookieWriter cookieWriter,
        IOptions<RefreshTokenOptions> options)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _cookieWriter = cookieWriter ?? throw new ArgumentNullException(nameof(cookieWriter));
        _cookieName = options.Value.CookieName;
    }

    /// <summary>
    /// Autentica a un usuario (SuperAdmin o miembro de Tenant) con sus credenciales.
    /// Emite la cookie HttpOnly <c>nalunpos_rt</c> con el refresh token inicial.
    /// </summary>
    /// <param name="request">Credenciales de inicio de sesión</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Respuesta con Token JWT y datos de perfil (cookie de refresh en la respuesta)</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] Pos.Api.Contracts.Requests.LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return await this.ToAuthResultAsync(result);
    }

    /// <summary>
    /// Rota el refresh token (endurecido: <c>[AllowAnonymous]</c>).
    /// Lee el token de la cookie <c>nalunpos_rt</c>, valida su hash SHA-256 en BD,
    /// rota atómicamente en la misma FamilyId y emite un nuevo access token + refresh token.
    /// En caso de token faltante, expirado o revocado → 401 y limpieza de la cookie.
    /// Reuso de token revocado → revoca toda la familia y 401.
    /// Tenant suspendido o inactivo → 403 sin consumir el token.
    /// Rotación concurrente dentro de la ventana de tolerancia → 409 sin revocar la
    /// familia y sin emitir cookie nueva (el cliente reintenta con la cookie vigente).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>200 con LoginResponse y nueva cookie; 401 con ProblemDetails y cookie limpiada; 403 por tenant suspendido/inactivo; 409 en rotación concurrente</returns>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        string? refreshToken = Request.Cookies[_cookieName];
        var command = new RefreshTokenCommand(refreshToken ?? string.Empty);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return await this.ToAuthResultAsync(result);
    }

    /// <summary>
    /// Cierra sesión de forma idempotente (<c>[AllowAnonymous]</c>).
    /// Revoca el refresh token de la cookie (si existe), borra la cookie y retorna 204.
    /// Siempre retorna 204 incluso si no hay cookie o el token ya está revocado.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>204 No Content</returns>
    [AllowAnonymous]
    [HttpPost("logout")]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        string? refreshToken = Request.Cookies[_cookieName];
        var command = new LogoutCommand(refreshToken);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        // Borrar la cookie siempre (idempotencia).
        _cookieWriter.Clear(Response);

        return this.ToNoContentResult(result);
    }

    /// <summary>
    /// Obtiene la identidad del usuario autenticado desde los claims del JWT.
    /// Requiere autenticación (<c>[Authorize]</c>). No consulta BD innecesariamente.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>200 con CurrentUserResponse; 401 si no autenticado o identidad inconsistente</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var query = new GetCurrentUserQuery();
        var currentUser = await _dispatcher.QueryAsync(query, cancellationToken);
        return Ok(currentUser);
    }

    [AllowAnonymous]
    [HttpGet("ping")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult Ping() => Ok("pong");
}
