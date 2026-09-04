using Pos.Application.Common.Interfaces;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Commands;

public record DeactivateSupplierCommand(Guid Id) : ICommand<bool>;

public class DeactivateSupplierCommandHandler : ICommandHandler<DeactivateSupplierCommand, bool>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateSupplierCommandHandler(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<bool> HandleAsync(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new SupplierNotFoundException(request.Id);

        supplier.Deactivate();
        _supplierRepository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
