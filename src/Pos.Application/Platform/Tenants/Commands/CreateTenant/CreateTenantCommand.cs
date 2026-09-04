using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Application.Platform.Tenants.Commands.CreateTenant;

/// <summary>
/// Comando para aprovisionar un nuevo Tenant (inquilino) y su usuario Administrador inicial por parte del SuperAdmin.
/// </summary>
public record CreateTenantCommand(
    string Name,
    string DocumentNumber,
    string AdminEmail,
    string AdminPassword) : ICommand<Result<Guid>>;
