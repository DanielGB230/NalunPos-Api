using Pos.Application.Common.Interfaces;
using Pos.Application.Purchases.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Purchases.Commands;

public record CompletePurchaseCommand(Guid Id) : ICommand<Result<PurchaseDto>>;

public class CompletePurchaseCommandHandler : ICommandHandler<CompletePurchaseCommand, Result<PurchaseDto>>
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDispatcher _dispatcher;

    public CompletePurchaseCommandHandler(
        IPurchaseRepository purchaseRepository,
        IUnitOfWork unitOfWork,
        IDispatcher dispatcher)
    {
        _purchaseRepository = purchaseRepository ?? throw new ArgumentNullException(nameof(purchaseRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<Result<PurchaseDto>> HandleAsync(CompletePurchaseCommand request, CancellationToken cancellationToken)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (purchase == null)
        {
            return Result.Fail<PurchaseDto>(DomainError.NotFound("Purchase.NotFound", $"No se encontró la orden de compra con el ID '{request.Id}'."));
        }

        try
        {
            purchase.Complete();
        }
        catch (DomainException ex)
        {
            return Result.Fail<PurchaseDto>(DomainError.Validation("Purchase.InvalidState", ex.Message));
        }

        _purchaseRepository.Update(purchase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Despachar eventos de dominio acumulados (PurchaseCompletedDomainEvent)
        foreach (var domainEvent in purchase.DomainEvents)
        {
            await _dispatcher.PublishAsync(domainEvent, cancellationToken);
        }
        purchase.ClearDomainEvents();

        return Result.Ok(PurchaseDto.FromEntity(purchase));
    }
}
