using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Queries;

[HasPermission(Permissions.Customers.View)]
public record GetCustomerByIdQuery(Guid Id) : IQuery<Result<CustomerDto>>;

[HasPermission(Permissions.Customers.View)]
public class GetCustomerByIdQueryHandler : IQueryHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomerByIdQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<Result<CustomerDto>> HandleAsync(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
        {
            return Result.Fail<CustomerDto>(DomainError.NotFound("Customer.NotFound", $"No se encontró el cliente con el ID '{request.Id}'."));
        }

        return Result.Ok(CustomerDto.FromEntity(customer));
    }
}
