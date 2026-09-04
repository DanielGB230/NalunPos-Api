using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Payments.Commands;

public record ProcessPaymentCommand(
    Guid SaleId,
    decimal Amount,
    PaymentMethod Method,
    string Currency = "USD",
    string? ExternalReference = null
) : ICommand<PaymentDto>;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la venta es requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto del pago debe ser mayor a cero.");

        RuleFor(x => x.Method)
            .IsInEnum().WithMessage("El método de pago especificado no es válido.");
    }
}

public class ProcessPaymentCommandHandler : ICommandHandler<ProcessPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public ProcessPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ISaleRepository saleRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _paymentGateway = paymentGateway ?? throw new ArgumentNullException(nameof(paymentGateway));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<PaymentDto> HandleAsync(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new SaleNotFoundException(request.SaleId);

        var amountMoney = Money.Create(request.Amount, request.Currency);
        var payment = Payment.Create(request.SaleId, amountMoney, request.Method, request.ExternalReference);

        // Invocar a la Capa Anti-Corrupción (ACL) para pasarela externa
        var gatewayResult = await _paymentGateway.ProcessPaymentAsync(payment, cancellationToken);

        if (gatewayResult.IsSuccess)
        {
            payment.Process(gatewayResult.TransactionId);
        }
        else
        {
            payment.Fail(gatewayResult.ErrorMessage ?? "Rechazado por la pasarela de pagos.");
        }

        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publicar eventos de dominio
        foreach (var domainEvent in payment.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        payment.ClearDomainEvents();

        return PaymentDto.FromEntity(payment);
    }
}
