using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Queries;

[HasPermission(Permissions.Suppliers.View)]
public record GetSupplierByIdQuery(Guid Id) : IQuery<Result<SupplierDto>>;

[HasPermission(Permissions.Suppliers.View)]
public class GetSupplierByIdQueryHandler : IQueryHandler<GetSupplierByIdQuery, Result<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSupplierByIdQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
    }

    public async Task<Result<SupplierDto>> HandleAsync(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier == null)
        {
            return Result.Fail<SupplierDto>(DomainError.NotFound("Supplier.NotFound", $"No se encontró el proveedor con el ID '{request.Id}'."));
        }

        return Result.Ok(SupplierDto.FromEntity(supplier));
    }
}
