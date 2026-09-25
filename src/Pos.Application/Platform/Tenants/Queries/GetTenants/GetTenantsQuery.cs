using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Tenants.DTOs;

namespace Pos.Application.Platform.Tenants.Queries.GetTenants;

/// <summary>
/// Query para obtener la lista paginada de Tenants registradas en la plataforma.
/// </summary>
[HasPermission(Permissions.Tenants.View)]
public record GetTenantsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null
) : IQuery<PagedResult<TenantDto>>;

/// <summary>
/// Handler de la consulta de Tenants paginada.
/// </summary>
[HasPermission(Permissions.Tenants.View)]
public class GetTenantsQueryHandler : IQueryHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetTenantsQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
    }

    public async Task<PagedResult<TenantDto>> HandleAsync(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);

        var (items, totalCount) = await _tenantRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            cancellationToken);

        return new PagedResult<TenantDto>(items, pageNumber, pageSize, totalCount);
    }
}
