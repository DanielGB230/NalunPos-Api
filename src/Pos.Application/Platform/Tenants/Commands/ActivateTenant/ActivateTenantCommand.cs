using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Platform.Tenants.Commands.ActivateTenant;

/// <summary>
/// Comando de plataforma para activar un Tenant (PendingProvisioning o Suspended → Active).
/// Exclusivo para SuperAdmin.
/// </summary>
[AllowsNoTenant]
[HasPermission(Permissions.Platform.Tenants.Update)]
public record ActivateTenantCommand(Guid Id) : ICommand<Result<bool>>;
