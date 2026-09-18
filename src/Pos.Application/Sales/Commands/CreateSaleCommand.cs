using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Sales.Commands;

public record CreateSaleItemDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPriceAmount
);

public record CreateSaleCommand(
    string ReceiptNumber,
    Guid SessionId,
    Guid? CustomerId,
    List<CreateSaleItemDto> LineItems,
    decimal TaxRatePercentage = 0m,
    string Currency = "USD"
) : ICommand<Result<SaleDto>>;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.ReceiptNumber)
            .NotEmpty().WithMessage("El número de comprobante es requerido.");

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("El ID de la sesión de caja es requerido.");

        RuleFor(x => x.LineItems)
            .NotEmpty().WithMessage("La venta debe incluir al menos un producto.");

        RuleForEach(x => x.LineItems).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId).NotEmpty().WithMessage("El ID del producto es requerido.");
            items.RuleFor(i => i.ProductName).NotEmpty().WithMessage("El nombre del producto es requerido.");
            items.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            items.RuleFor(i => i.UnitPriceAmount).GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
        });
    }
}

public class CreateSaleCommandHandler : ICommandHandler<CreateSaleCommand, Result<SaleDto>>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ICashRegisterRepository _registerRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public CreateSaleCommandHandler(
        ISaleRepository saleRepository,
        ICashRegisterRepository registerRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository,
        IStockLevelRepository stockLevelRepository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result<SaleDto>> HandleAsync(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validar sesión de caja abierta
        var session = await _registerRepository.GetSessionByIdAsync(request.SessionId, cancellationToken);
        if (session == null)
        {
            return Result.Fail<SaleDto>(DomainError.NotFound(
                "CashRegisterSession.NotFound",
                $"No se encontró la sesión de caja con ID '{request.SessionId}'."));
        }

        if (session.Status != SessionStatus.Open)
        {
            return Result.Fail<SaleDto>(DomainError.Conflict(
                "CashRegisterSession.Closed",
                "La sesión de caja especificada no se encuentra abierta."));
        }

        // 2. Resolver Almacén para la venta (Almacén por defecto del Tenant)
        var warehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
        if (warehouse is null)
        {
            var warehouses = await _warehouseRepository.GetAllAsync(cancellationToken);
            warehouse = warehouses.Count > 0 ? warehouses[0] : null;
        }

        if (warehouse is null)
        {
            return Result.Fail<SaleDto>(DomainError.NotFound(
                "Warehouse.NotFound",
                "No se encontró un almacén activo o por defecto para procesar la venta."));
        }

        // 3. Validar cliente si fue especificado
        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            var customer = await _customerRepository.GetByIdAsync(request.CustomerId.Value, cancellationToken);
            if (customer == null)
            {
                return Result.Fail<SaleDto>(DomainError.NotFound(
                    "Customer.NotFound",
                    $"No se encontró el cliente especificado con ID '{request.CustomerId.Value}'."));
            }
        }

        // 4. Validar existencia de productos y disponibilidad en StockLevel
        foreach (var item in request.LineItems)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product == null)
            {
                return Result.Fail<SaleDto>(DomainError.NotFound(
                    "Product.NotFound",
                    $"No se encontró el producto con ID '{item.ProductId}'."));
            }

            if (!product.IsActive)
            {
                return Result.Fail<SaleDto>(DomainError.Conflict(
                    "Product.Inactive",
                    $"El producto '{product.Name}' está inactivo y no puede ser vendido."));
            }

            var stockLevel = await _stockLevelRepository.GetAsync(product.Id, warehouse.Id, null, cancellationToken);
            decimal available = stockLevel?.QuantityAvailable ?? 0m;

            if (available < item.Quantity)
            {
                return Result.Fail<SaleDto>(DomainError.Validation(
                    "StockLevel.InsufficientStock",
                    $"Stock insuficiente para '{product.Name}' en almacén '{warehouse.Name}'. Disponible: {available}, solicitado: {item.Quantity}."));
            }
        }

        // 5. Crear la entidad Venta
        Sale sale;
        try
        {
            var lineItems = request.LineItems.Select(item => SaleLineItem.Create(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                Money.Create(item.UnitPriceAmount, request.Currency)
            )).ToList();

            sale = Sale.Create(
                request.ReceiptNumber,
                request.SessionId,
                request.CustomerId,
                lineItems,
                request.TaxRatePercentage,
                request.Currency);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SaleDto>(DomainError.Validation("Sale.Invalid", ex.Message));
        }

        await _saleRepository.AddAsync(sale, cancellationToken);

        // 6. Descontar StockLevel y registrar Kardex (ADR-Inventory-001) por cada producto
        foreach (var item in request.LineItems)
        {
            var stockLevel = await _stockLevelRepository.GetAsync(item.ProductId, warehouse.Id, null, cancellationToken);
            stockLevel!.Decrement(item.Quantity);
            _stockLevelRepository.Update(stockLevel);

            var movement = InventoryMovement.Record(
                productId: item.ProductId,
                warehouseId: warehouse.Id,
                quantity: -item.Quantity,
                movementType: InventoryMovementType.Sale,
                referenceId: sale.Id,
                notes: $"Venta N° {request.ReceiptNumber}");

            await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
        }

        // 7. UNICO COMMIT ATÓMICO: Persistir Venta, StockLevels e InventoryMovements en una sola transacción
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Despachar eventos de dominio acumulados
        foreach (var domainEvent in sale.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        sale.ClearDomainEvents();

        return Result.Ok(SaleDto.FromEntity(sale));
    }
}
