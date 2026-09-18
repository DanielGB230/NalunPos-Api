using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockAdjustments.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.StockAdjustments.Commands;

public record CreateStockAdjustmentLineDto(Guid ProductId, decimal Quantity);

public record CreateStockAdjustmentCommand(
    Guid WarehouseId,
    StockAdjustmentReason Reason,
    List<CreateStockAdjustmentLineDto> Lines,
    string? Notes = null
) : ICommand<Result<StockAdjustmentDto>>;

public class CreateStockAdjustmentCommandValidator : AbstractValidator<CreateStockAdjustmentCommand>
{
    public CreateStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("El ID del almacén es requerido.");
        RuleFor(x => x.Reason).IsInEnum().WithMessage("El motivo del ajuste es requerido.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("El ajuste debe tener al menos una línea.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).NotEqual(0).WithMessage("La cantidad de ajuste no puede ser cero.");
        });
    }
}

public class CreateStockAdjustmentCommandHandler : ICommandHandler<CreateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjustmentRepository;
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjustmentRepository,
        IStockLevelRepository stockLevelRepository,
        IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _adjustmentRepository = adjustmentRepository ?? throw new ArgumentNullException(nameof(adjustmentRepository));
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<StockAdjustmentDto>> HandleAsync(CreateStockAdjustmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = await _warehouseRepository.GetByIdAsync(command.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Fail<StockAdjustmentDto>(DomainError.NotFound("Warehouse.NotFound", $"No se encontró el almacén con ID '{command.WarehouseId}'."));

        Guid tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // 1. Validar todos los productos ANTES de mutar cualquier estado
        foreach (var line in command.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null)
                return Result.Fail<StockAdjustmentDto>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con ID '{line.ProductId}'."));

            // Para salidas, validar stock suficiente
            if (line.Quantity < 0)
            {
                var stockLevel = await _stockLevelRepository.GetAsync(line.ProductId, command.WarehouseId, null, cancellationToken);
                decimal available = stockLevel?.QuantityAvailable ?? 0m;
                if (available < Math.Abs(line.Quantity))
                    return Result.Fail<StockAdjustmentDto>(DomainError.Validation(
                        "StockLevel.InsufficientStock",
                        $"Stock insuficiente para '{product.Name}'. Disponible: {available}, ajuste solicitado: {line.Quantity}."));
            }
        }

        // 2. Crear el agregado StockAdjustment (dominio)
        StockAdjustment adjustment;
        try
        {
            var lineItems = command.Lines.Select(l => (l.ProductId, l.Quantity));
            adjustment = StockAdjustment.Create(tenantId, command.WarehouseId, command.Reason, lineItems, command.Notes);
        }
        catch (DomainException ex)
        {
            return Result.Fail<StockAdjustmentDto>(DomainError.Validation("StockAdjustment.Invalid", ex.Message));
        }

        await _adjustmentRepository.AddAsync(adjustment, cancellationToken);

        // 3. REGLA ATÓMICA (ADR-Inventory-001):
        //    Por cada línea: StockLevel + InventoryMovement en la misma transacción
        foreach (var line in command.Lines)
        {
            var stockLevel = await _stockLevelRepository.GetAsync(line.ProductId, command.WarehouseId, null, cancellationToken);
            bool isNew = stockLevel is null;
            if (isNew)
            {
                stockLevel = StockLevel.Create(tenantId, line.ProductId, command.WarehouseId);
                await _stockLevelRepository.AddAsync(stockLevel, cancellationToken);
            }

            if (line.Quantity > 0)
                stockLevel!.Increment(line.Quantity);
            else
                stockLevel!.Decrement(Math.Abs(line.Quantity));

            if (!isNew)
            {
                _stockLevelRepository.Update(stockLevel!);
            }

            var movement = InventoryMovement.Record(
                productId: line.ProductId,
                warehouseId: command.WarehouseId,
                quantity: line.Quantity,
                movementType: InventoryMovementType.Adjustment,
                referenceId: adjustment.Id,
                notes: $"Ajuste: {command.Reason} — {command.Notes ?? string.Empty}".TrimEnd(' ', '—'));

            await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
        }

        // 4. ÚNICO SaveChangesAsync
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(StockAdjustmentDto.FromEntity(adjustment));
    }
}
