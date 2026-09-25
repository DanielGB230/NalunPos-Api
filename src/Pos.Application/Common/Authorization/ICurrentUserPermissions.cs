using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pos.Application.Common.Authorization;

public interface ICurrentUserPermissions
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid? tenantId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(Guid? tenantId, Guid roleId, string permission, CancellationToken cancellationToken = default);
}
