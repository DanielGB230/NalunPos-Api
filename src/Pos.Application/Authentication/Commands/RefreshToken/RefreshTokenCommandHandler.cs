using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Options;
using Pos.Application.Authentication.Commands.Login;
using Pos.Domain.Authentication;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.RefreshToken;

/// <summary>
/// Handler de rotación de refresh token.
/// Valida el hash SHA-256 en BD, rota atómicamente en la misma FamilyId, re-valida
/// usuario/rol fail-closed (ADR 0023) y emite un nuevo access token + refresh token.
/// </summary>
public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IAuthUserLookup _authUserLookup;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITenantStatusReader _tenantStatusReader;
    private readonly TimeProvider _timeProvider;
    private readonly RefreshTokenOptions _options;

    public RefreshTokenCommandHandler(
        IRefreshTokenStore refreshTokenStore,
        IRefreshTokenGenerator refreshTokenGenerator,
        IAuthUserLookup authUserLookup,
        ITokenGenerator tokenGenerator,
        ITenantStatusReader tenantStatusReader,
        TimeProvider timeProvider,
        RefreshTokenOptions options)
    {
        _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        _refreshTokenGenerator = refreshTokenGenerator ?? throw new ArgumentNullException(nameof(refreshTokenGenerator));
        _authUserLookup = authUserLookup ?? throw new ArgumentNullException(nameof(authUserLookup));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _tenantStatusReader = tenantStatusReader ?? throw new ArgumentNullException(nameof(tenantStatusReader));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<Result<LoginResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var invalid = DomainError.Unauthorized(
            "Auth.RefreshTokenInvalid",
            "Token de actualización inválido, expirado o revocado.");

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return invalid;
        }

        string tokenHash = ComputeSha256(command.RefreshToken);

        // 2. Buscar el token por hash para determinar su estado.
        var candidate = await _refreshTokenStore.FindByHashAsync(tokenHash, cancellationToken);
        if (candidate is null)
        {
            return invalid; // Token desconocido
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // 3. Reuso de token revocado → revocar toda la familia y 401.
        if (candidate.IsRevoked)
        {
            await _refreshTokenStore.RevokeFamilyAsync(
                candidate.FamilyId,
                RefreshTokenRevocationReasons.ReuseDetected,
                nowUtc,
                cancellationToken);
            return invalid;
        }

        // 4. Token expirado → revocarlo y 401 (sin revocar familia).
        if (nowUtc >= candidate.ExpiresAtUtc)
        {
            await _refreshTokenStore.RevokeAsync(
                tokenHash,
                RefreshTokenRevocationReasons.Expired,
                nowUtc,
                cancellationToken);
            return invalid;
        }

        // 5. Token activo: rotación atómica (revocar actual + insertar nuevo en misma familia).
        var material = _refreshTokenGenerator.Generate();
        var rotation = await _refreshTokenStore.RotateAsync(
            tokenHash,
            material.TokenHash,
            nowUtc,
            _options.RefreshTokenLifetime,
            cancellationToken);

        if (!rotation.Success)
        {
            // Concurrencia: el token ya fue consumido por otra petición.
            if (rotation.ReuseDetected)
            {
                await _refreshTokenStore.RevokeFamilyAsync(
                    candidate.FamilyId,
                    RefreshTokenRevocationReasons.ReuseDetected,
                    nowUtc,
                    cancellationToken);
            }
            return invalid;
        }

        // 6. Re-validar usuario y rol fail-closed (ADR 0023).
        var authUser = await _authUserLookup.FindByIdAsync(candidate.UserId, cancellationToken);
        if (authUser is null || !authUser.User.IsActive)
        {
            return DomainError.Unauthorized(
                "Auth.UserInvalid",
                "El usuario asociado al refresh token no existe o está inactivo.");
        }

        // Control de acceso por estado de tenant (solo usuarios de tenant).
        if (authUser.User.TenantId.HasValue)
        {
            var tenantStatus = await _tenantStatusReader.GetStatusAsync(authUser.User.TenantId.Value, cancellationToken);
            var accessError = TenantAccessPolicy.ValidateAccess(tenantStatus);
            if (accessError is not null)
            {
                return Result.Fail<LoginResponse>(accessError);
            }
        }

        // 7. Emitir nuevo access token.
        AccessTokenResult accessToken = _tokenGenerator.GenerateToken(authUser.User, authUser.Role);

        var response = new LoginResponse(
            Token: accessToken.Token,
            UserId: authUser.User.Id,
            RoleId: authUser.User.RoleId,
            Role: authUser.Role,
            Email: authUser.User.Email.Value,
            FullName: authUser.User.FullName,
            TenantId: authUser.User.TenantId,
            ExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshToken: material.Token,
            RefreshTokenMaxAgeSeconds: (int)_options.RefreshTokenLifetime.TotalSeconds);

        return response;
    }

    private static string ComputeSha256(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
