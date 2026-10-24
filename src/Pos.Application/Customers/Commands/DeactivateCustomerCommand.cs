using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Commands;

[RequiresTenant]
[HasPermission(Permissions.Customers.Update)]
public record DeactivateCustomerCommand(Guid Id) : ICommand<Result<bool>>;

public class DeactivateCustomerCommandHandler : ICommandHandler<DeactivateCustomerCommand, Result<bool>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DeactivateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<bool>> HandleAsync(DeactivateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Customer.NotFound", $"No se encontró el cliente con el ID '{request.Id}'."));
        }

        if (!customer.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Customer.AlreadyInactive", $"El cliente con ID '{request.Id}' ya se encuentra inactivo."));
        }

        customer.Deactivate(_timeProvider);
        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
