using FluentValidation;
using Pos.Application.Authentication.DTOs;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Authentication.Queries;

public record LoginQuery(string Email, string Password) : IQuery<AuthResponseDto>;

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

public class LoginQueryHandler : IQueryHandler<LoginQuery, AuthResponseDto>
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

    public async Task<AuthResponseDto> HandleAsync(LoginQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedDomainException("Credenciales de acceso inválidas o usuario inactivo.");
        }

        bool isValidPassword = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value);
        if (!isValidPassword)
        {
            throw new UnauthorizedDomainException("Credenciales de acceso inválidas.");
        }

        string token = _jwtTokenGenerator.GenerateToken(user, null!);

        return new AuthResponseDto(
            token,
            UserDto.FromEntity(user),
            new List<string>()
        );
    }
}
