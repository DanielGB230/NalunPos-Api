using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Roles.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Roles.Commands;

[HasPermission(Pos.Application.Common.Authorization.Permissions.Roles.Create)]
public record CreateRoleCommand(
    string Name,
    string? Description = null,
    List<string>? Permissions = null
) : ICommand<Result<RoleDto>>;



public class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, Result<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _currentTenantContext;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork, ICurrentTenantContext currentTenantContext)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentTenantContext = currentTenantContext ?? throw new ArgumentNullException(nameof(currentTenantContext));
    }

    public async Task<Result<RoleDto>> HandleAsync(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        bool nameExists = await _roleRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (nameExists)
        {
            return Result.Fail<RoleDto>(DomainError.Conflict("Role.AlreadyExists", $"Ya existe un rol con el nombre '{request.Name}'."));
        }

        if (!_currentTenantContext.TenantId.HasValue)
        {
            return Result.Fail<RoleDto>(DomainError.Validation("Role.TenantIdRequired", "TenantId es requerido para crear un rol."));
        }

        Role role;
        try
        {
            role = Role.Create(_currentTenantContext.TenantId.Value, request.Name, request.Description, request.Permissions);
        }
        catch (DomainException ex)
        {
            return Result.Fail<RoleDto>(DomainError.Validation("Role.Invalid", ex.Message));
        }

        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(RoleDto.FromEntity(role));
    }
}
