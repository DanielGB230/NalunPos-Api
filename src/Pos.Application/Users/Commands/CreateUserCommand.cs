using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Users.Commands;

public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role,
    Guid? TenantId
) : ICommand<Result<UserDto>>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es requerido.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es requerido.")
            .EmailAddress().WithMessage("El correo no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.");
    }
}

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<UserDto>> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        bool emailExists = await _userRepository.ExistsByEmailAsync(request.Email, null, cancellationToken);
        if (emailExists)
        {
            return Result.Fail<UserDto>(DomainError.Conflict("User.AlreadyExists", $"Ya existe un usuario registrado con el correo '{request.Email}'."));
        }

        string passwordHash = _passwordHasher.HashPassword(request.Password);

        User user;
        try
        {
            user = User.Create(
                new Email(request.Email),
                new PasswordHash(passwordHash),
                request.Role,
                request.TenantId,
                request.FirstName,
                request.LastName);
        }
        catch (DomainException ex)
        {
            return Result.Fail<UserDto>(DomainError.Validation("User.Invalid", ex.Message));
        }

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(UserDto.FromEntity(user));
    }
}
