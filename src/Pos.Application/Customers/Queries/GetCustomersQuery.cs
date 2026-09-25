using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Customers.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// </summary>
[HasPermission(Permissions.Customers.View)]
public record GetCustomersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? IsActive = null
) : IQuery<PagedResult<CustomerDto>>;

[HasPermission(Permissions.Customers.View)]
public class GetCustomersQueryHandler : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    }

    public async Task<PagedResult<CustomerDto>> HandleAsync(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var (items, totalCount) = await _customerRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(CustomerDto.FromEntity).ToList();

        return new PagedResult<CustomerDto>(dtos, pageNumber, pageSize, totalCount);
    }
}
