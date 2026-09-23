using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// El controlador construye esta query a partir del GetCustomersRequest.
/// </summary>
public record GetCustomersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActive = null
) : IQuery<PagedResult<CustomerDto>>;

public class GetCustomersQueryHandler : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<PagedResult<CustomerDto>> HandleAsync(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _customerRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(CustomerDto.FromEntity).ToList();

        return new PagedResult<CustomerDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
