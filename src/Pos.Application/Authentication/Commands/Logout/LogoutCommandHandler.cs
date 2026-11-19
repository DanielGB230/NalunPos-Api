using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;
using Pos.Domain.Common;

namespace Pos.Application.Authentication.Commands.Logout;

/// <summary>
/// Handler de logout. Revoca el refresh token de la cookie (si existe) y
/// retorna éxito siempre (idempotente). El borrado de la cookie lo hace el
/// controlador en la respuesta HTTP.
/// </summary>
public class LogoutCommandHandler : ICommandHandler<LogoutCommand, Result>
{
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly TimeProvider _timeProvider;

    public LogoutCommandHandler(
        IRefreshTokenStore refreshTokenStore,
        TimeProvider timeProvider)
    {
        _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            string tokenHash = ComputeSha256(command.RefreshToken);
            await _refreshTokenStore.RevokeAsync(
                tokenHash,
                RefreshTokenRevocationReasons.Logout,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);
        }

        return Result.Success();
    }

    private static string ComputeSha256(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
