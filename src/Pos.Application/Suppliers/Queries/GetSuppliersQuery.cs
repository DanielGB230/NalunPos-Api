using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// El controlador construye esta query a partir del GetSuppliersRequest.
/// </summary>
public record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActive = null
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
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(SupplierDto.FromEntity).ToList();

        return new PagedResult<SupplierDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
