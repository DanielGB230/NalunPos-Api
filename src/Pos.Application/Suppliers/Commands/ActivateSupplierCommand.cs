using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Commands;

[HasPermission(Permissions.Suppliers.Update)]
public record ActivateSupplierCommand(Guid Id) : ICommand<Result<bool>>;

[HasPermission(Permissions.Suppliers.Update)]
public class ActivateSupplierCommandHandler : ICommandHandler<ActivateSupplierCommand, Result<bool>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateSupplierCommandHandler(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(ActivateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Supplier.NotFound", $"No se encontró el proveedor con el ID '{request.Id}'."));
        }

        if (supplier.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Supplier.AlreadyActive", $"El proveedor con ID '{request.Id}' ya se encuentra activo."));
        }

        supplier.Activate();
        _supplierRepository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
