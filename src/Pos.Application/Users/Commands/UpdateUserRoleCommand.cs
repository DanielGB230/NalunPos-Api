using FluentValidation;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Commands;

[HasPermission(Permissions.Users.Update)]
public record UpdateUserRoleCommand(
    Guid Id,
    Guid RoleId,
    Guid? TenantId = null
) : ICommand<Result<UserDto>>;

public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del usuario es requerido.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("El RoleId no puede estar vacío.");
    }
}

public class UpdateUserRoleCommandHandler : ICommandHandler<UpdateUserRoleCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserRoleCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
        {
            return Result.Fail<UserDto>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        try
        {
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
