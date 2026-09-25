using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Platform.Tenants.Commands.CreateTenant;

/// <summary>
/// Comando para aprovisionar un nuevo Tenant (inquilino) y su usuario Administrador inicial por parte del SuperAdmin.
/// </summary>
[HasPermission(Permissions.Tenants.Create)]
public record CreateTenantCommand(
    string Name,
    string DocumentNumber,
    string AdminEmail,
    string AdminPassword) : ICommand<Result<Guid>>;
