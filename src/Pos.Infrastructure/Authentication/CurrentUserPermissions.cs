using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Pos.Application.Common.Authorization;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

namespace Pos.Infrastructure.Authentication;

public sealed class CurrentUserPermissions : ICurrentUserPermissions
{
    private readonly HybridCache _cache;
    private readonly IRoleRepository _roleRepository;

    public CurrentUserPermissions(HybridCache cache, IRoleRepository roleRepository)
    {
        _cache = cache;
        _roleRepository = roleRepository;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"permissions:{tenantId?.ToString() ?? "global"}:{roleId}";

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(60)
        };

        var permissions = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var role = await _roleRepository.GetByIdAsync(roleId, cancel);
                if (role == null)
                {
                    return new HashSet<string>();
                }

                if (role.Id == Role.SuperAdminRoleId)
                {
                    return new HashSet<string>(role.Permissions);
                }

                if (role.TenantId != tenantId)
                {
                    return new HashSet<string>();
                }

                return new HashSet<string>(role.Permissions);
            },
            entryOptions,
            cancellationToken: cancellationToken);

        return permissions;
    }

    public async Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default)
    {
        var permissions = await GetPermissionsAsync(tenantId, roleId, cancellationToken);
        return permissions.Contains(permission);
    }
}
