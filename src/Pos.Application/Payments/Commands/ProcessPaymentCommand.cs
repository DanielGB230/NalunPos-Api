using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Payments.Commands;

[HasPermission(Permissions.Payments.Process)]
public record ProcessPaymentCommand(
    Guid SaleId,
    decimal Amount,
    PaymentMethod Method,
    string Currency = "USD",
    string? ExternalReference = null
) : ICommand<Result<PaymentDto>>;

[HasPermission(Permissions.Payments.Process)]
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

[HasPermission(Permissions.Payments.Process)]
public class ProcessPaymentCommandHandler : ICommandHandler<ProcessPaymentCommand, Result<PaymentDto>>
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

    public async Task<Result<PaymentDto>> HandleAsync(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sale = await _saleRepository.GetByIdAsync(request.SaleId, cancellationToken);
        if (sale == null)
        {
            return Result.Fail<PaymentDto>(DomainError.NotFound(
                "Sale.NotFound",
                $"No se encontró la venta especificada con ID '{request.SaleId}'."));
        }

        Payment payment;
        try
        {
            var amountMoney = Money.Create(request.Amount, request.Currency);
            payment = Payment.Create(request.SaleId, amountMoney, request.Method, request.ExternalReference);
        }
        catch (DomainException ex)
        {
            return Result.Fail<PaymentDto>(DomainError.Validation("Payment.Invalid", ex.Message));
        }

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

        if (!gatewayResult.IsSuccess)
        {
            return Result.Fail<PaymentDto>(DomainError.Validation(
                "Payment.GatewayDeclined",
                gatewayResult.ErrorMessage ?? "El pago fue rechazado por la entidad financiera."));
        }

        return Result.Ok(PaymentDto.FromEntity(payment));
    }
}
