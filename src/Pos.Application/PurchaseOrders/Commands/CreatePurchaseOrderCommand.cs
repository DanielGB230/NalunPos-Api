using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.PurchaseOrders.Commands;

public record CreatePurchaseOrderLineDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCostAmount,
    string Currency = "USD"
);

[HasPermission(Permissions.PurchaseOrders.Create)]
public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    List<CreatePurchaseOrderLineDto> Lines,
    string? Notes = null
) : ICommand<Result<PurchaseOrderDto>>;

[HasPermission(Permissions.PurchaseOrders.Create)]
public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("El ID del proveedor es requerido.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("El ID del almacén destino es requerido.");
        RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(50).WithMessage("El número de orden es requerido.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("La orden debe contener al menos una línea.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty();
            l.RuleFor(i => i.Quantity).GreaterThan(0);
            l.RuleFor(i => i.UnitCostAmount).GreaterThanOrEqualTo(0);
        });
    }
}

[HasPermission(Permissions.PurchaseOrders.Create)]
public class CreatePurchaseOrderCommandHandler : ICommandHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _orderRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository orderRepository,
        ISupplierRepository supplierRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId, cancellationToken);
        if (supplier is null)
            return Result.Fail<PurchaseOrderDto>(DomainError.NotFound("Supplier.NotFound", $"No se encontró el proveedor con ID '{command.SupplierId}'."));

        if (!supplier.IsActive)
            return Result.Fail<PurchaseOrderDto>(DomainError.Conflict("Supplier.Inactive", $"El proveedor '{supplier.Name}' está inactivo y no se le pueden crear órdenes de compra."));

        var warehouse = await _warehouseRepository.GetByIdAsync(command.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Fail<PurchaseOrderDto>(DomainError.NotFound("Warehouse.NotFound", $"No se encontró el almacén con ID '{command.WarehouseId}'."));

        foreach (var line in command.Lines)
        {
            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null)
                return Result.Fail<PurchaseOrderDto>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con ID '{line.ProductId}'."));

            if (!product.IsActive)
                return Result.Fail<PurchaseOrderDto>(DomainError.Conflict("Product.Inactive", $"El producto '{product.Name}' está inactivo y no se puede añadir a una orden de compra."));
        }

        Guid tenantId = _tenantContext.TenantId ?? Guid.Empty;

        PurchaseOrder order;
        try
        {
            var lines = command.Lines.Select(l =>
                (l.ProductId, l.Quantity, Money.Create(l.UnitCostAmount, l.Currency)));

            order = PurchaseOrder.Create(tenantId, command.SupplierId, command.WarehouseId, command.OrderNumber, lines, command.Notes);
        }
        catch (DomainException ex)
        {
            return Result.Fail<PurchaseOrderDto>(DomainError.Validation("PurchaseOrder.Invalid", ex.Message));
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(PurchaseOrderDto.FromEntity(order));
    }
}
