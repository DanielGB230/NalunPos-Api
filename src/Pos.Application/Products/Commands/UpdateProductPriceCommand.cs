using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Products.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Products.Commands;

public record UpdateProductPriceCommand(
    Guid ProductId,
    decimal PriceAmount,
    string Currency,
    decimal? CostAmount = null
) : ICommand<Result<ProductDto>>;

public class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El ID del producto es requerido.");

        RuleFor(x => x.PriceAmount)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser mayor o igual a 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La divisa es requerida.")
            .Length(3).WithMessage("La divisa debe ser un código ISO de 3 caracteres.");
    }
}

public class UpdateProductPriceCommandHandler : ICommandHandler<UpdateProductPriceCommand, Result<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductPriceCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<ProductDto>> HandleAsync(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Fail<ProductDto>(DomainError.NotFound(
                "Product.NotFound",
                $"No se encontró el producto con ID '{request.ProductId}'."));
        }

        try
        {
            var newPrice = Money.Create(request.PriceAmount, request.Currency);
            var newCost = request.CostAmount.HasValue ? Money.Create(request.CostAmount.Value, request.Currency) : null;

            product.UpdatePrice(newPrice, newCost);
        }
        catch (DomainException ex)
        {
            return Result.Fail<ProductDto>(DomainError.Validation("Product.InvalidPrice", ex.Message));
        }

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(ProductDto.FromEntity(product));
    }
}
