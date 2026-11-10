using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Platform.Tenants.Commands.CreateTenant;

/// <summary>
/// Handler del caso de uso de aprovisionamiento de nuevos Tenants y su usuario Administrador inicial por parte del SuperAdmin.
/// </summary>
public class CreateTenantCommandHandler : ICommandHandler<CreateTenantCommand, Result<Guid>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuthUserLookup _authUserLookup;
    private readonly IBranchRepository _branchRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IAuthUserLookup authUserLookup,
        IBranchRepository branchRepository,
        IWarehouseRepository warehouseRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _authUserLookup = authUserLookup ?? throw new ArgumentNullException(nameof(authUserLookup));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<Guid>> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool tenantExists = await _tenantRepository.ExistsByTaxIdAsync(command.DocumentNumber, cancellationToken);
        if (tenantExists)
        {
            return Result.Fail<Guid>(DomainError.Conflict(
                "Tenant.AlreadyExists",
                $"Ya existe una empresa registrada con el documento '{command.DocumentNumber}'."));
        }

        bool emailExists = await _authUserLookup.ExistsByEmailAsync(command.AdminEmail, null, cancellationToken);
        if (emailExists)
        {
            return Result.Fail<Guid>(DomainError.Conflict(
                "User.EmailAlreadyExists",
                $"El correo electrónico '{command.AdminEmail}' ya se encuentra registrado en el sistema."));
        }

        Tenant tenant;
        User adminUser;
        Branch defaultBranch;
        Warehouse defaultWarehouse;
        List<Role> systemRoles;
        try
        {
            tenant = Tenant.Create(command.Name, command.DocumentNumber, _timeProvider);
            string passwordHash = _passwordHasher.HashPassword(command.AdminPassword);

            systemRoles = DefaultTenantRoleCatalog.All
                .Select(def => Role.CreateSystemRole(tenant.Id, def.Name, def.Description, def.Permissions, _timeProvider))
                .ToList();

            Role adminRole = systemRoles.Single(r =>
                string.Equals(r.Name, TenantRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase));

            adminUser = User.Create(
                command.AdminEmail,
                passwordHash,
                adminRole.Id,
                tenant.Id,
                firstName: command.Name,
                lastName: "Admin",
                _timeProvider);

            // Auto-generar Sucursal y Almacén por defecto para el tenant
            var address = Pos.Domain.ValueObjects.Address.Create("Principal", "Central", "Central", "00000");
            defaultBranch = Branch.Create(tenant.Id, $"{command.Name} - Principal", address, _timeProvider);
            defaultWarehouse = Warehouse.Create(
                tenant.Id,
                defaultBranch.Id,
                "Almacén Principal",
                "Almacén principal predeterminado del negocio",
                isDefault: true,
                _timeProvider);
        }
        catch (DomainException ex)
        {
            return Result.Fail<Guid>(DomainError.Validation("Tenant.Invalid", ex.Message));
        }

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        foreach (var role in systemRoles)
        {
            await _roleRepository.AddAsync(role, cancellationToken);
        }
        await _userRepository.AddAsync(adminUser, cancellationToken);
        await _branchRepository.AddAsync(defaultBranch, cancellationToken);
        await _warehouseRepository.AddAsync(defaultWarehouse, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(tenant.Id);
    }
}
