using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Suppliers.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// </summary>
[HasPermission(Permissions.Suppliers.View)]
public record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? IsActive = null
) : IQuery<PagedResult<SupplierDto>>;

[HasPermission(Permissions.Suppliers.View)]
public class GetSuppliersQueryHandler : IQueryHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;

    public GetSuppliersQueryHandler(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
    }

    public async Task<PagedResult<SupplierDto>> HandleAsync(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var (items, totalCount) = await _supplierRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(SupplierDto.FromEntity).ToList();

        return new PagedResult<SupplierDto>(dtos, pageNumber, pageSize, totalCount);
    }
}
