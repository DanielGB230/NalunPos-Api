using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Products.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Products.Queries;

[HasPermission(Permissions.Products.View)]
public record GetProductByIdQuery(Guid Id) : IQuery<Result<ProductDto>>;

[HasPermission(Permissions.Products.View)]
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Fail<ProductDto>(DomainError.NotFound(
                "Product.NotFound",
                $"No se encontró el producto con ID '{request.Id}'."));
        }

        return Result.Ok(ProductDto.FromEntity(product));
    }
}
