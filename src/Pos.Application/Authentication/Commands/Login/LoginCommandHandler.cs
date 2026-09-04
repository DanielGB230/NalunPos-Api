using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Authentication.Commands.Login;

/// <summary>
/// Handler del comando de inicio de sesión.
/// Retorna Result<LoginResponse> en lugar de lanzar excepciones por credenciales inválidas.
/// </summary>
public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
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

        var user = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Mismo mensaje genérico por seguridad (evita la enumeración de usuarios)
            return invalidCredentialsError;
        }

        bool isPasswordValid = _passwordHasher.Verify(command.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return invalidCredentialsError;
        }

        string token = _tokenGenerator.GenerateToken(user);
        DateTime expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var response = new LoginResponse(
            Token: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: user.Id,
            Role: user.Role,
            TenantId: user.TenantId
        );

        return response;
    }
}
