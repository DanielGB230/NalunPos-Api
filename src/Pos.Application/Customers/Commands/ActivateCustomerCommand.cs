using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Commands;

[RequiresTenant]
[HasPermission(Permissions.Customers.Update)]
public record ActivateCustomerCommand(Guid Id) : ICommand<Result<bool>>;

public class ActivateCustomerCommandHandler : ICommandHandler<ActivateCustomerCommand, Result<bool>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ActivateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<bool>> HandleAsync(ActivateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Customer.NotFound", $"No se encontró el cliente con el ID '{request.Id}'."));
        }

        if (customer.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Customer.AlreadyActive", $"El cliente con ID '{request.Id}' ya se encuentra activo."));
        }

        customer.Activate(_timeProvider);
        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
