using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.PurchaseOrders.Commands;

[HasPermission(Permissions.PurchaseOrders.Send)]
public record SendPurchaseOrderCommand(Guid PurchaseOrderId) : ICommand<Result<PurchaseOrderDto>>;

[HasPermission(Permissions.PurchaseOrders.Send)]
public class SendPurchaseOrderCommandHandler : ICommandHandler<SendPurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SendPurchaseOrderCommandHandler(
        IPurchaseOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<PurchaseOrderDto>> HandleAsync(SendPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _orderRepository.GetByIdAsync(command.PurchaseOrderId, cancellationToken);
        if (order is null)
            return Result.Fail<PurchaseOrderDto>(DomainError.NotFound("PurchaseOrder.NotFound", $"No se encontró la orden con ID '{command.PurchaseOrderId}'."));

        try
        {
            order.Send();
        }
        catch (DomainException ex)
        {
            return Result.Fail<PurchaseOrderDto>(DomainError.Conflict("PurchaseOrder.InvalidState", ex.Message));
        }

        _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(PurchaseOrderDto.FromEntity(order));
    }
}
