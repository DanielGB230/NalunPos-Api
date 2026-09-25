using FluentValidation;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Users.Commands;

[HasPermission(Permissions.Users.Update)]
public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Guid RoleId,
    Guid? TenantId
) : ICommand<Result<UserDto>>;

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

public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthUserLookup _authUserLookup;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuthUserLookup authUserLookup)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _authUserLookup = authUserLookup ?? throw new ArgumentNullException(nameof(authUserLookup));
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
        {
            return Result.Fail<UserDto>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        bool emailExists = await _authUserLookup.ExistsByEmailAsync(request.Email, request.Id, cancellationToken);
        if (emailExists)
        {
            return Result.Fail<UserDto>(DomainError.Conflict("User.AlreadyExists", $"Ya existe otro usuario registrado con el correo '{request.Email}'."));
        }

        try
        {
            user.UpdateDetails(request.FirstName, request.LastName);
            user.UpdateEmail(new Email(request.Email));
            user.ChangeRole(request.RoleId, request.TenantId);
        }
        catch (DomainException ex)
        {
            return Result.Fail<UserDto>(DomainError.Validation("User.Invalid", ex.Message));
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(UserDto.FromEntity(user));
    }
}
