using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Products.Commands;

[HasPermission(Permissions.Products.Update)]
public record DeactivateProductCommand(Guid Id) : ICommand<Result<bool>>;

[HasPermission(Permissions.Products.Update)]
public class DeactivateProductCommandHandler : ICommandHandler<DeactivateProductCommand, Result<bool>>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(DeactivateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con el ID '{request.Id}'."));
        }

        if (!product.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Product.AlreadyInactive", $"El producto con ID '{request.Id}' ya se encuentra inactivo."));
        }

        product.Deactivate();
        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
