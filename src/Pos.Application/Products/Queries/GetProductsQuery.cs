using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Products.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Products.Queries;

public record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? CategoryId = null,
    bool? IsActiveOnly = null
) : IQuery<PagedResult<ProductDto>>;

public class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<PagedResult<ProductDto>> HandleAsync(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _productRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.CategoryId,
            request.IsActiveOnly,
            cancellationToken);

        var dtos = items.Select(ProductDto.FromEntity).ToList();

        return new PagedResult<ProductDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
