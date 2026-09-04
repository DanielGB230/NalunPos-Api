using Pos.Application.Common.Interfaces;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Queries;

public record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto>;

public class GetCustomerByIdQueryHandler : IQueryHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomerByIdQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<CustomerDto> HandleAsync(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CustomerNotFoundException(request.Id);

        return CustomerDto.FromEntity(customer);
    }
}
