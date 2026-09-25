using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Commands;

[HasPermission(Permissions.Users.Update)]
public record DeactivateUserCommand(Guid Id) : ICommand<Result<bool>>;

public class DeactivateUserCommandHandler : ICommandHandler<DeactivateUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        if (!user.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("User.AlreadyInactive", $"El usuario con ID '{request.Id}' ya se encuentra inactivo."));
        }

        user.Deactivate();
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
