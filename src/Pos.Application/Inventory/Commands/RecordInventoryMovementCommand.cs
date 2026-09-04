using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Commands;

public record RecordInventoryMovementCommand(
    Guid ProductId,
    decimal Quantity,
    InventoryMovementType MovementType,
    Guid? ReferenceId = null,
    string? Notes = null
) : ICommand<InventoryMovementDto>;

public class RecordInventoryMovementCommandValidator : AbstractValidator<RecordInventoryMovementCommand>
{
    public RecordInventoryMovementCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("El ID del producto es requerido.");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("La cantidad de movimiento no puede ser cero.");

        RuleFor(x => x.MovementType)
            .IsInEnum().WithMessage("El tipo de movimiento especificado no es válido.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Las notas no pueden exceder 500 caracteres.");
    }
}

public class RecordInventoryMovementCommandHandler : ICommandHandler<RecordInventoryMovementCommand, InventoryMovementDto>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordInventoryMovementCommandHandler(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<InventoryMovementDto> HandleAsync(RecordInventoryMovementCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(request.ProductId);

        // Si es una salida, verificar stock suficiente en Kardex
        if (request.Quantity < 0)
        {
            decimal currentStock = await _inventoryRepository.GetCurrentStockAsync(request.ProductId, cancellationToken);
            if (currentStock + request.Quantity < 0)
            {
                throw new DomainException($"Stock Kardex insuficiente para el producto '{product.Name}'. Stock actual: {currentStock}, ajuste: {request.Quantity}.");
            }
        }

        var movement = InventoryMovement.Record(
            request.ProductId,
            request.Quantity,
            request.MovementType,
            request.ReferenceId,
            request.Notes);

        // Actualiza la proyección del Agregado de Producto
        product.AdjustStock((int)request.Quantity);

        await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
        _productRepository.Update(product);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return InventoryMovementDto.FromEntity(movement);
    }
}
