using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Roles.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Roles.Commands;

public record CreateRoleCommand(
    string Name,
    string? Description = null,
    List<string>? Permissions = null
) : ICommand<RoleDto>;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es requerido.")
            .Length(2, 50).WithMessage("El nombre debe contener entre 2 y 50 caracteres.");
    }
}

public class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRoleCommandHandler(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<RoleDto> HandleAsync(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        bool nameExists = await _roleRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (nameExists)
        {
            throw new DomainException($"Ya existe un rol con el nombre '{request.Name}'.");
        }

        var role = Role.Create(request.Name, request.Description, request.Permissions);

        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RoleDto.FromEntity(role);
    }
}
