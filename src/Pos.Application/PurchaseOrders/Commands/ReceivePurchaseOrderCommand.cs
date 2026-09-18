using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.PurchaseOrders.Commands;

public record ReceivePurchaseOrderLineDto(
    Guid ProductId,
    decimal ReceivedQuantity,
    string? BatchNumber = null,
    DateTime? ExpirationDate = null
);

public record ReceivePurchaseOrderCommand(
    Guid PurchaseOrderId,
    List<ReceivePurchaseOrderLineDto> ReceivedLines
) : ICommand<Result<PurchaseOrderDto>>;

public class ReceivePurchaseOrderCommandValidator : AbstractValidator<ReceivePurchaseOrderCommand>
{
    public ReceivePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty().WithMessage("El ID de la orden es requerido.");
        RuleFor(x => x.ReceivedLines).NotEmpty().WithMessage("Debe especificar al menos una línea a recibir.");
        RuleForEach(x => x.ReceivedLines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.ReceivedQuantity).GreaterThan(0);
        });
    }
}

public class ReceivePurchaseOrderCommandHandler : ICommandHandler<ReceivePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _orderRepository;
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public ReceivePurchaseOrderCommandHandler(
        IPurchaseOrderRepository orderRepository,
        IStockLevelRepository stockLevelRepository,
        IInventoryRepository inventoryRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(ReceivePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _orderRepository.GetByIdAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
            return Result.Fail<PurchaseOrderDto>(DomainError.NotFound("PurchaseOrder.NotFound", $"No se encontró la orden con ID '{command.PurchaseOrderId}'."));

        Guid tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // 1. Actualizar el dominio — ReceiveLines valida invariantes y retorna líneas afectadas
        IReadOnlyList<PurchaseOrderLine> updatedLines;
        try
        {
            var receivedItems = command.ReceivedLines.Select(r => (r.ProductId, r.ReceivedQuantity));
            updatedLines = order.ReceiveLines(receivedItems);
        }
        catch (DomainException ex)
        {
            return Result.Fail<PurchaseOrderDto>(DomainError.Conflict("PurchaseOrder.ReceiveError", ex.Message));
        }

        _orderRepository.Update(order);

        // 2. REGLA ATÓMICA (ADR-Inventory-001):
        //    Por cada línea recibida: StockLevel.Increment + InventoryMovement.Record
        //    Todo en la misma transacción — SaveChangesAsync se llama UNA SOLA VEZ al final.
        foreach (var line in updatedLines)
        {
            var receivedDto = command.ReceivedLines.First(r => r.ProductId == line.ProductId);

            // 2a. Obtener o crear StockLevel para este producto × almacén
            var stockLevel = await _stockLevelRepository.GetAsync(line.ProductId, order.WarehouseId, null, cancellationToken);
            bool isNew = stockLevel is null;
            if (isNew)
            {
                stockLevel = StockLevel.Create(tenantId, line.ProductId, order.WarehouseId);
                await _stockLevelRepository.AddAsync(stockLevel, cancellationToken);
            }

            // 2b. Incrementar stock disponible
            stockLevel!.Increment(receivedDto.ReceivedQuantity);
            if (!isNew)
            {
                _stockLevelRepository.Update(stockLevel!);
            }

            // 2c. Registrar en el Kardex (append-only)
            var movement = InventoryMovement.Record(
                productId: line.ProductId,
                warehouseId: order.WarehouseId,
                quantity: receivedDto.ReceivedQuantity,         // Positivo = entrada
                movementType: InventoryMovementType.Purchase,
                referenceId: order.Id,
                notes: $"Recepción OC {order.OrderNumber}",
                batchNumber: receivedDto.BatchNumber,
                expirationDate: receivedDto.ExpirationDate);

            await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
        }

        // 3. ÚNICO SaveChangesAsync — commit atómico de todo lo anterior
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(PurchaseOrderDto.FromEntity(order));
    }
}
