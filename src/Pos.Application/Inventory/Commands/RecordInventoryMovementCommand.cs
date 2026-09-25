using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Commands;

[HasPermission(Permissions.Inventory.AdjustStock)]
public record RecordInventoryMovementCommand(
    Guid ProductId,
    Guid? WarehouseId,
    decimal Quantity,
    InventoryMovementType MovementType,
    Guid? ReferenceId = null,
    string? Notes = null
) : ICommand<Result<InventoryMovementDto>>;

[HasPermission(Permissions.Inventory.AdjustStock)]
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

[HasPermission(Permissions.Inventory.AdjustStock)]
public class RecordInventoryMovementCommandHandler : ICommandHandler<RecordInventoryMovementCommand, Result<InventoryMovementDto>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public RecordInventoryMovementCommandHandler(
        IInventoryRepository inventoryRepository,
        IStockLevelRepository stockLevelRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<InventoryMovementDto>> HandleAsync(RecordInventoryMovementCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Fail<InventoryMovementDto>(DomainError.NotFound(
                "Product.NotFound",
                $"No se encontró el producto con ID '{request.ProductId}'."));
        }

        Guid warehouseId = request.WarehouseId ?? Guid.Empty;
        if (warehouseId == Guid.Empty)
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            warehouseId = defaultWarehouse?.Id ?? Guid.Empty;
        }

        if (warehouseId == Guid.Empty)
        {
            return Result.Fail<InventoryMovementDto>(DomainError.NotFound(
                "Warehouse.NotFound",
                "No se encontró un almacén activo o por defecto para registrar el movimiento."));
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);

        Guid tenantId = (_tenantContext.TenantId.HasValue && _tenantContext.TenantId.Value != Guid.Empty)
            ? _tenantContext.TenantId.Value
            : (warehouse?.TenantId ?? product.TenantId);

        if (tenantId == Guid.Empty)
            tenantId = Guid.NewGuid();

        var stockLevel = await _stockLevelRepository.GetAsync(product.Id, warehouseId, null, cancellationToken);

        // Si es una salida, verificar stock suficiente en StockLevel
        if (request.Quantity < 0)
        {
            decimal available = stockLevel?.QuantityAvailable ?? 0m;
            if (available < Math.Abs(request.Quantity))
            {
                return Result.Fail<InventoryMovementDto>(DomainError.Validation(
                    "StockLevel.InsufficientStock",
                    $"Stock insuficiente para '{product.Name}'. Disponible: {available}, solicitado: {request.Quantity}."));
            }
        }

        bool isNew = stockLevel is null;
        if (isNew)
        {
            stockLevel = StockLevel.Create(tenantId, product.Id, warehouseId);
            await _stockLevelRepository.AddAsync(stockLevel, cancellationToken);
        }

        InventoryMovement movement;
        try
        {
            if (request.Quantity > 0)
                stockLevel!.Increment(request.Quantity);
            else
                stockLevel!.Decrement(Math.Abs(request.Quantity));

            if (!isNew)
            {
                _stockLevelRepository.Update(stockLevel!);
            }

            movement = InventoryMovement.Record(
                productId: request.ProductId,
                warehouseId: warehouseId,
                quantity: request.Quantity,
                movementType: request.MovementType,
                referenceId: request.ReferenceId,
                notes: request.Notes);
        }
        catch (DomainException ex)
        {
            return Result.Fail<InventoryMovementDto>(DomainError.Validation("Inventory.Invalid", ex.Message));
        }

        await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(InventoryMovementDto.FromEntity(movement));
    }
}
