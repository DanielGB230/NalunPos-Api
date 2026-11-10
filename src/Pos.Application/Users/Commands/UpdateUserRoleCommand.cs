using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Users.Commands;

[RequiresTenant]
[HasPermission(Permissions.Users.Update)]
public record UpdateUserRoleCommand(
    Guid Id,
    Guid RoleId
) : ICommand<Result<UserDto>>;

public class UpdateUserRoleCommandHandler : ICommandHandler<UpdateUserRoleCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public UpdateUserRoleCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantContext.RequireTenantId();

        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null || user.TenantId != tenantId)
        {
            return Result.Fail<UserDto>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.Id}'."));
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null || role.TenantId != tenantId || role.Id == Role.SuperAdminRoleId)
        {
            return Result.Fail<UserDto>(DomainError.NotFound(
                "Role.NotFound",
                $"No se encontró el rol con el ID '{request.RoleId}' en el tenant actual."));
        }

        try
        {
            user.ChangeRole(request.RoleId, _timeProvider);
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
