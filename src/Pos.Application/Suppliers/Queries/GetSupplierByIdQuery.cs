using Pos.Application.Common.Interfaces;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Queries;

public record GetSupplierByIdQuery(Guid Id) : IQuery<SupplierDto>;

public class GetSupplierByIdQueryHandler : IQueryHandler<GetSupplierByIdQuery, SupplierDto>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSupplierByIdQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
    }

    public async Task<SupplierDto> HandleAsync(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new SupplierNotFoundException(request.Id);

        return SupplierDto.FromEntity(supplier);
    }
}
