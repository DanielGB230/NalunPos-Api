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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public CreateSaleCommandHandler(
        ISaleRepository saleRepository,
        ICashRegisterRepository registerRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
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

        // 2. Validar cliente si fue especificado
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

        // 3. Validar existencia de productos, stock en Kardex y descontar stock de forma síncrona
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

            decimal currentStock = await _inventoryRepository.GetCurrentStockAsync(product.Id, cancellationToken);
            if (currentStock < item.Quantity)
            {
                return Result.Fail<SaleDto>(DomainError.Validation(
                    "Inventory.InsufficientStock",
                    $"Stock insuficiente para el producto '{product.Name}'. Stock disponible en Kardex: {currentStock}, cantidad solicitada: {item.Quantity}."));
            }

            // Descontar stock del Agregado Product
            try
            {
                product.AdjustStock(-(int)item.Quantity);
            }
            catch (DomainException ex)
            {
                return Result.Fail<SaleDto>(DomainError.Validation("Inventory.Invalid", ex.Message));
            }

            // Registrar movimiento de Kardex síncronamente
            var movement = InventoryMovement.Record(
                product.Id,
                -item.Quantity,
                InventoryMovementType.Sale,
                notes: $"Salida por Venta N° {request.ReceiptNumber}"
            );

            await _inventoryRepository.AddMovementAsync(movement, cancellationToken);
            _productRepository.Update(product);
        }

        // 4. Crear la entidad Venta
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

        // 5. UNICO COMMIT ATÓMICO: Persistir Venta, Movimientos de Inventario y Actualización de Stock en una sola transacción
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Despachar eventos de dominio acumulados (para efectos secundarios no bloqueantes)
        foreach (var domainEvent in sale.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        sale.ClearDomainEvents();

        return Result.Ok(SaleDto.FromEntity(sale));
    }
}
