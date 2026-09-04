using Pos.Application.Common.Interfaces;
using Pos.Application.Roles.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Roles.Queries;

public record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public class GetRolesQueryHandler : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;

    public GetRolesQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    }

    public async Task<IReadOnlyList<RoleDto>> HandleAsync(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var items = await _roleRepository.GetAllAsync(cancellationToken);
        return items.Select(RoleDto.FromEntity).ToList();
    }
}
