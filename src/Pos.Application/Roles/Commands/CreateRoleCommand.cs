using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using FluentValidation;
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

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es requerido.")
            .Length(2, 50).WithMessage("El nombre debe contener entre 2 y 50 caracteres.");
    }
}

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

        Role role;
        try
        {
            var tenantId = _currentTenantContext.TenantId ?? throw new DomainException("TenantId is required.");
            role = Role.Create(tenantId, request.Name, request.Description, request.Permissions);
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
