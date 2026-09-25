using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Authentication.DTOs;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Authentication.Queries;

[PublicUseCase]
public record LoginQuery(string Email, string Password) : IQuery<Result<AuthResponseDto>>;

[PublicUseCase]
public class LoginQueryValidator : AbstractValidator<LoginQuery>
{
    public LoginQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.");
    }
}

[PublicUseCase]
public class LoginQueryHandler : IQueryHandler<LoginQuery, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginQueryHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _jwtTokenGenerator = jwtTokenGenerator ?? throw new ArgumentNullException(nameof(jwtTokenGenerator));
    }

    public async Task<Result<AuthResponseDto>> HandleAsync(LoginQuery request, CancellationToken cancellationToken)
    {
        var invalidCredentialsError = DomainError.Unauthorized(
            "Auth.InvalidCredentials",
            "Credenciales de acceso inválidas.");

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return Result.Fail<AuthResponseDto>(invalidCredentialsError);
        }

        bool isValidPassword = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value);
        if (!isValidPassword)
        {
            return Result.Fail<AuthResponseDto>(invalidCredentialsError);
        }

        string token = _jwtTokenGenerator.GenerateToken(user, null!);

        return Result.Ok(new AuthResponseDto(
            token,
            UserDto.FromEntity(user),
            new List<string>()
        ));
    }
}
