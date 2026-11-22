using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Commands;

[RequiresTenant]
[HasPermission(Permissions.Users.Update)]
public record ActivateUserCommand(Guid Id) : ICommand<Result<bool>>;

public class ActivateUserCommandHandler : ICommandHandler<ActivateUserCommand, Result<bool>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSessionRevoker _userSessionRevoker;
    private readonly TimeProvider _timeProvider;

    public ActivateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IUserSessionRevoker userSessionRevoker)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _userSessionRevoker = userSessionRevoker ?? throw new ArgumentNullException(nameof(userSessionRevoker));
    }

    public async Task<Result<bool>> HandleAsync(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        if (user.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("User.AlreadyActive", $"El usuario con ID '{request.Id}' ya se encuentra activo."));
        }

        // Se revoca antes de guardar para cerrar el hueco de una revocación fallida
        // tras desactivar: el reintento de desactivar devolvería AlreadyInactive.
        await _userSessionRevoker.RevokeAllAsync(
            user.Id,
            RefreshTokenRevocationReasons.UserReactivated,
            cancellationToken);

        user.Activate(_timeProvider);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
