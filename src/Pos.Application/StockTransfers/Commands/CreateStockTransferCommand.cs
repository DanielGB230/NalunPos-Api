using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockTransfers.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.StockTransfers.Commands;

public record CreateStockTransferLineDto(Guid ProductId, decimal Quantity);

[HasPermission(Permissions.StockTransfers.Create)]
public record CreateStockTransferCommand(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    List<CreateStockTransferLineDto> Lines,
    string? Notes = null
) : ICommand<Result<StockTransferDto>>;

[HasPermission(Permissions.StockTransfers.Create)]
public class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.SourceWarehouseId).NotEmpty().WithMessage("El ID del almacén de origen es requerido.");
        RuleFor(x => x.DestinationWarehouseId).NotEmpty().WithMessage("El ID del almacén de destino es requerido.");
        RuleFor(x => x)
            .Must(x => x.SourceWarehouseId != x.DestinationWarehouseId)
            .WithMessage("El almacén de origen y destino no pueden ser el mismo.");

        RuleFor(x => x.Lines).NotEmpty().WithMessage("El traspaso debe contener al menos una línea de producto.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad a traspasar debe ser mayor a cero.");
        });
    }
}

[HasPermission(Permissions.StockTransfers.Create)]
public class CreateStockTransferCommandHandler : ICommandHandler<CreateStockTransferCommand, Result<StockTransferDto>>
{
    private readonly IStockTransferRepository _transferRepository;
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStockTransferCommandHandler(
        IStockTransferRepository transferRepository,
        IStockLevelRepository stockLevelRepository,
        IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _transferRepository = transferRepository ?? throw new ArgumentNullException(nameof(transferRepository));
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<StockTransferDto>> HandleAsync(CreateStockTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SourceWarehouseId == command.DestinationWarehouseId)
            return Result.Fail<StockTransferDto>(DomainError.Validation(
                "StockTransfer.SameWarehouse",
                "El almacén de origen y el almacén de destino deben ser diferentes."));

        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(command.SourceWarehouseId, cancellationToken);
        if (sourceWarehouse is null)
            return Result.Fail<StockTransferDto>(DomainError.NotFound("Warehouse.SourceNotFound", $"No se encontró el almacén de origen con ID '{command.SourceWarehouseId}'."));

        var destWarehouse = await _warehouseRepository.GetByIdAsync(command.DestinationWarehouseId, cancellationToken);
        if (destWarehouse is null)
            return Result.Fail<StockTransferDto>(DomainError.NotFound("Warehouse.DestinationNotFound", $"No se encontró el almacén de destino con ID '{command.DestinationWarehouseId}'."));

        Guid tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // 1. Validar existencia de productos y stock disponible en ORIGEN para TODAS las líneas antes de mutar
        foreach (var line in command.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null)
                return Result.Fail<StockTransferDto>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con ID '{line.ProductId}'."));

            var sourceStock = await _stockLevelRepository.GetAsync(line.ProductId, command.SourceWarehouseId, null, cancellationToken);
            decimal available = sourceStock?.QuantityAvailable ?? 0m;
            if (available < line.Quantity)
            {
                return Result.Fail<StockTransferDto>(DomainError.Validation(
                    "StockLevel.InsufficientStock",
                    $"Stock insuficiente para '{product.Name}' en el almacén '{sourceWarehouse.Name}'. Disponible: {available}, requerido: {line.Quantity}."));
            }
        }

        // 2. Crear agregado StockTransfer
        StockTransfer transfer;
        try
        {
            var lineItems = command.Lines.Select(l => (l.ProductId, l.Quantity));
            transfer = StockTransfer.Create(tenantId, command.SourceWarehouseId, command.DestinationWarehouseId, lineItems, command.Notes);
        }
        catch (DomainException ex)
        {
            return Result.Fail<StockTransferDto>(DomainError.Validation("StockTransfer.Invalid", ex.Message));
        }

        await _transferRepository.AddAsync(transfer, cancellationToken);

        // 3. REGLA ATÓMICA DE TRASPASO (ADR-Inventory-001):
        //    Por cada línea:
        //      a) Decrementar StockLevel Origen + InventoryMovement(TransferOut) en Origen
        //      b) Incrementar StockLevel Destino + InventoryMovement(TransferIn) en Destino
        foreach (var line in command.Lines)
        {
            // ORIGEN
            var sourceStock = await _stockLevelRepository.GetAsync(line.ProductId, command.SourceWarehouseId, null, cancellationToken);
            sourceStock!.Decrement(line.Quantity);
            _stockLevelRepository.Update(sourceStock);

            var movementOut = InventoryMovement.Record(
                productId: line.ProductId,
                warehouseId: command.SourceWarehouseId,
                quantity: -line.Quantity,
                movementType: InventoryMovementType.TransferOut,
                referenceId: transfer.Id,
                notes: $"Traspaso hacia '{destWarehouse.Name}' — {command.Notes ?? string.Empty}".TrimEnd(' ', '—'));

            await _inventoryRepository.AddMovementAsync(movementOut, cancellationToken);

            // DESTINO
            var destStock = await _stockLevelRepository.GetAsync(line.ProductId, command.DestinationWarehouseId, null, cancellationToken);
            bool isDestNew = destStock is null;
            if (isDestNew)
            {
                destStock = StockLevel.Create(tenantId, line.ProductId, command.DestinationWarehouseId);
                await _stockLevelRepository.AddAsync(destStock, cancellationToken);
            }

            destStock!.Increment(line.Quantity);
            if (!isDestNew)
            {
                _stockLevelRepository.Update(destStock!);
            }

            var movementIn = InventoryMovement.Record(
                productId: line.ProductId,
                warehouseId: command.DestinationWarehouseId,
                quantity: line.Quantity,
                movementType: InventoryMovementType.TransferIn,
                referenceId: transfer.Id,
                notes: $"Traspaso desde '{sourceWarehouse.Name}' — {command.Notes ?? string.Empty}".TrimEnd(' ', '—'));

            await _inventoryRepository.AddMovementAsync(movementIn, cancellationToken);
        }

        // 4. ÚNICO SaveChangesAsync — Garantiza 100% de atomicidad bidireccional
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(StockTransferDto.FromEntity(transfer));
    }
}
