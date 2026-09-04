using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Queries;

public record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActiveOnly = null
) : IQuery<PagedResult<SupplierDto>>;

public class GetSuppliersQueryHandler : IQueryHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSuppliersQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
    }

    public async Task<PagedResult<SupplierDto>> HandleAsync(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _supplierRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActiveOnly,
            cancellationToken);

        var dtos = items.Select(SupplierDto.FromEntity).ToList();

        return new PagedResult<SupplierDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
