using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Platform.Tenants.Commands.SuspendTenant;

/// <summary>
/// Comando de plataforma para suspender un Tenant (Active → Suspended).
/// Exclusivo para SuperAdmin.
/// </summary>
[AllowsNoTenant]
[HasPermission(Permissions.Platform.Tenants.Update)]
public record SuspendTenantCommand(Guid Id) : ICommand<Result<bool>>;
