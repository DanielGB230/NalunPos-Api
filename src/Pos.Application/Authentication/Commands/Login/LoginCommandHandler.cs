using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Options;
using Pos.Domain.Common;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// Handler del comando de inicio de sesión.
/// Retorna Result<LoginResponse> en lugar de lanzar excepciones por credenciales inválidas.
/// Emite y persiste el refresh token inicial (hash SHA-256) en una nueva familia.
/// </summary>
public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IAuthUserLookup _authUserLookup;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITenantStatusReader _tenantStatusReader;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly RefreshTokenOptions _options;

    public LoginCommandHandler(
        IAuthUserLookup authUserLookup,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ITenantStatusReader tenantStatusReader,
        IRefreshTokenStore refreshTokenStore,
        IRefreshTokenGenerator refreshTokenGenerator,
        TimeProvider timeProvider,
        RefreshTokenOptions options)
    {
        _authUserLookup = authUserLookup ?? throw new ArgumentNullException(nameof(authUserLookup));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _tenantStatusReader = tenantStatusReader ?? throw new ArgumentNullException(nameof(tenantStatusReader));
        _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        _refreshTokenGenerator = refreshTokenGenerator ?? throw new ArgumentNullException(nameof(refreshTokenGenerator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var invalidCredentialsError = DomainError.Unauthorized(
            "Auth.InvalidCredentials",
            "Credenciales de acceso inválidas.");

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return invalidCredentialsError;
        }

        var authUser = await _authUserLookup.FindByEmailAsync(command.Email, cancellationToken);
        if (authUser is null || !authUser.User.IsActive)
        {
            // Mismo mensaje genérico por seguridad (evita la enumeración de usuarios)
            return invalidCredentialsError;
        }

        var user = authUser.User;
        var roleName = authUser.Role;

        bool isPasswordValid = _passwordHasher.Verify(command.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return invalidCredentialsError;
        }

        // ── Control de acceso por estado de tenant (después de validar credenciales) ──
        // Solo usuarios de tenant (TenantId != null) están sujetos a esta regla.
        // SuperAdmin (TenantId == null) no se ve afectado.
        if (user.TenantId.HasValue)
        {
            var tenantStatus = await _tenantStatusReader.GetStatusAsync(user.TenantId.Value, cancellationToken);
            var accessError = TenantAccessPolicy.ValidateAccess(tenantStatus);
            if (accessError is not null)
            {
                // No se emite token; se devuelve el error de denegación con su código estable.
                return Result.Fail<LoginResponse>(accessError);
            }
        }

        AccessTokenResult accessToken = _tokenGenerator.GenerateToken(user, roleName);

        // ── Emitir refresh token inicial (rotación: nueva familia) ──
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        Guid familyId = Guid.CreateVersion7();
        var refreshMaterial = _refreshTokenGenerator.Generate();
        DateTime issuedExpiresAtUtc = await _refreshTokenStore.IssueAsync(
            user.Id,
            familyId,
            refreshMaterial.TokenHash,
            _options.RefreshTokenLifetime,
            _options.FamilyAbsoluteLifetime,
            cancellationToken);

        var response = new LoginResponse(
            Token: accessToken.Token,
            UserId: user.Id,
            RoleId: user.RoleId,
            Role: roleName,
            Email: user.Email.Value,
            FullName: user.FullName,
            TenantId: user.TenantId,
            ExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshToken: refreshMaterial.Token,
            RefreshTokenMaxAgeSeconds: ComputeMaxAgeSeconds(issuedExpiresAtUtc, nowUtc));

        return response;
    }

    /// <summary>
    /// Max-Age de la cookie calculado desde el vencimiento efectivo del token emitido.
    /// Nunca negativo y con un mínimo de 1 segundo (F1.G.6).
    /// </summary>
    private static int ComputeMaxAgeSeconds(DateTime expiresAtUtc, DateTime nowUtc)
    {
        double seconds = (expiresAtUtc - nowUtc).TotalSeconds;
        return seconds < 1 ? 1 : (int)Math.Ceiling(seconds);
    }
}
