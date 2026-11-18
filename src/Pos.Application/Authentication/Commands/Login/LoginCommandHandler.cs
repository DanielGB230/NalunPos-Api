using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// Handler del comando de inicio de sesión.
/// Retorna Result<LoginResponse> en lugar de lanzar excepciones por credenciales inválidas.
/// </summary>
public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IAuthUserLookup _authUserLookup;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITenantStatusReader _tenantStatusReader;
    private readonly TimeProvider _timeProvider;

    public LoginCommandHandler(
        IAuthUserLookup authUserLookup,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ITenantStatusReader tenantStatusReader,
        TimeProvider timeProvider)
    {
        _authUserLookup = authUserLookup ?? throw new ArgumentNullException(nameof(authUserLookup));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _tenantStatusReader = tenantStatusReader ?? throw new ArgumentNullException(nameof(tenantStatusReader));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

        string token = _tokenGenerator.GenerateToken(user, roleName);
        DateTime expiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddHours(1);

        var response = new LoginResponse(
            Token: token,
            UserId: user.Id,
            RoleId: user.RoleId,
            Role: roleName,
            Email: user.Email.Value,
            FullName: user.FullName,
            TenantId: user.TenantId,
            ExpiresAtUtc: expiresAtUtc
        );

        return response;
    }
}
