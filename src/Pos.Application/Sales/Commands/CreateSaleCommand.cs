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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public CreateSaleCommandHandler(
        ISaleRepository saleRepository,
        ICashRegisterRepository registerRepository,
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result<SaleDto>> HandleAsync(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Despachar eventos de dominio acumulados (SaleCompletedDomainEvent)
        foreach (var domainEvent in sale.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        sale.ClearDomainEvents();

        return Result.Ok(SaleDto.FromEntity(sale));
    }
}
