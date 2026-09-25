using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Products.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Products.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// </summary>
[HasPermission(Permissions.Products.View)]
public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    Guid? CategoryId = null,
    bool? IsActive = null
) : IQuery<PagedResult<ProductDto>>;

[HasPermission(Permissions.Products.View)]
public class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<PagedResult<ProductDto>> HandleAsync(GetProductsQuery request, CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var (items, totalCount) = await _productRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.CategoryId,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(ProductDto.FromEntity).ToList();

        return new PagedResult<ProductDto>(dtos, pageNumber, pageSize, totalCount);
    }
}
