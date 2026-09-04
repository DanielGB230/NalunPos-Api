using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Purchases.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Purchases.Commands;

public record CreatePurchaseItemDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPriceAmount
);

public record CreatePurchaseCommand(
    Guid SupplierId,
    string OrderNumber,
    List<CreatePurchaseItemDto> LineItems,
    string Currency = "USD"
) : ICommand<PurchaseDto>;

public class CreatePurchaseCommandValidator : AbstractValidator<CreatePurchaseCommand>
{
    public CreatePurchaseCommandValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("El ID del proveedor es requerido.");

        RuleFor(x => x.OrderNumber)
            .NotEmpty().WithMessage("El número de orden de compra es requerido.");

        RuleFor(x => x.LineItems)
            .NotEmpty().WithMessage("La orden de compra debe incluir al menos un producto.");

        RuleForEach(x => x.LineItems).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId).NotEmpty().WithMessage("El ID del producto es requerido.");
            items.RuleFor(i => i.ProductName).NotEmpty().WithMessage("El nombre del producto es requerido.");
            items.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            items.RuleFor(i => i.UnitPriceAmount).GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
        });
    }
}

public class CreatePurchaseCommandHandler : ICommandHandler<CreatePurchaseCommand, PurchaseDto>
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseCommandHandler(
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository,
        IUnitOfWork unitOfWork)
    {
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<PurchaseDto> HandleAsync(CreatePurchaseCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken)
            ?? throw new SupplierNotFoundException(request.SupplierId);

        var lineItems = request.LineItems.Select(item => PurchaseLineItem.Create(
            item.ProductId,
            item.ProductName,
            item.Quantity,
            Money.Create(item.UnitPriceAmount, request.Currency)
        )).ToList();

        var purchase = Purchase.Create(
            request.SupplierId,
            request.OrderNumber,
            lineItems,
            request.Currency);

        await _purchaseRepository.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PurchaseDto.FromEntity(purchase);
    }
}
