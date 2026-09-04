using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? TenantId,
    bool IsActive
) : ICommand<UserDto>;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del usuario es requerido.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es requerido.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es requerido.")
            .EmailAddress().WithMessage("El correo no es válido.");
    }
}

public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> HandleAsync(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new UserNotFoundException(request.Id);

        bool emailExists = await _userRepository.ExistsByEmailAsync(request.Email, request.Id, cancellationToken);
        if (emailExists)
        {
            throw new DomainException($"Ya existe otro usuario registrado con el correo '{request.Email}'.");
        }

        user.UpdateDetails(request.FirstName, request.LastName);
        user.UpdateEmail(new Email(request.Email));
        user.ChangeRole(request.Role, request.TenantId);

        if (request.IsActive && !user.IsActive)
        {
            user.Activate();
        }
        else if (!request.IsActive && user.IsActive)
        {
            user.Deactivate();
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.FromEntity(user);
    }
}
