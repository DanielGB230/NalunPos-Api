using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Products.Commands;

[HasPermission(Permissions.Products.Update)]
public record ActivateProductCommand(Guid Id) : ICommand<Result<bool>>;

[HasPermission(Permissions.Products.Update)]
public class ActivateProductCommandHandler : ICommandHandler<ActivateProductCommand, Result<bool>>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(ActivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con el ID '{request.Id}'."));
        }

        if (product.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Product.AlreadyActive", $"El producto con ID '{request.Id}' ya se encuentra activo."));
        }

        product.Activate();
        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
