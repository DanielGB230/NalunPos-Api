using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Sales.DTOs;
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
) : ICommand<SaleDto>;

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

public class CreateSaleCommandHandler : ICommandHandler<CreateSaleCommand, SaleDto>
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

    public async Task<SaleDto> HandleAsync(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var session = await _registerRepository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new CashRegisterSessionNotFoundException(request.SessionId);

        if (session.Status != SessionStatus.Open)
        {
            throw new DomainException("La sesión de caja especificada no se encuentra abierta.");
        }

        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            var customer = await _customerRepository.GetByIdAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new CustomerNotFoundException(request.CustomerId.Value);
        }

        var lineItems = request.LineItems.Select(item => SaleLineItem.Create(
            item.ProductId,
            item.ProductName,
            item.Quantity,
            Money.Create(item.UnitPriceAmount, request.Currency)
        )).ToList();

        var sale = Sale.Create(
            request.ReceiptNumber,
            request.SessionId,
            request.CustomerId,
            lineItems,
            request.TaxRatePercentage,
            request.Currency);

        await _saleRepository.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Despachar eventos de dominio acumulados (SaleCompletedDomainEvent)
        foreach (var domainEvent in sale.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        sale.ClearDomainEvents();

        return SaleDto.FromEntity(sale);
    }
}
