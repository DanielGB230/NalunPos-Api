using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
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
    private readonly IBranchRepository _branchRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IBranchRepository branchRepository,
        IWarehouseRepository warehouseRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        bool emailExists = await _userRepository.ExistsByEmailAsync(command.AdminEmail, null, cancellationToken);
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

        try
        {
            tenant = Tenant.Create(command.Name, command.DocumentNumber);
            string passwordHash = _passwordHasher.HashPassword(command.AdminPassword);

            adminUser = User.Create(
                command.AdminEmail,
                passwordHash,
                UserRole.TenantAdmin,
                tenant.Id,
                firstName: command.Name,
                lastName: "Admin");

            // Auto-generar Sucursal y Almacén por defecto para el tenant
            var address = Pos.Domain.ValueObjects.Address.Create("Principal", "Central", "Central", "00000");
            defaultBranch = Branch.Create(tenant.Id, $"{command.Name} - Principal", address);
            defaultWarehouse = Warehouse.Create(
                tenant.Id,
                defaultBranch.Id,
                "Almacén Principal",
                "Almacén principal predeterminado del negocio",
                isDefault: true);
        }
        catch (DomainException ex)
        {
            return Result.Fail<Guid>(DomainError.Validation("Tenant.Invalid", ex.Message));
        }

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _userRepository.AddAsync(adminUser, cancellationToken);
        await _branchRepository.AddAsync(defaultBranch, cancellationToken);
        await _warehouseRepository.AddAsync(defaultWarehouse, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(tenant.Id);
    }
}
