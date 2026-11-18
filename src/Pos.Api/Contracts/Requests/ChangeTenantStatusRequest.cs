using Pos.Domain.Enums;

namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato HTTP para cambiar el estado de un Tenant desde el control plane.
/// El cuerpo solo admite <see cref="TenantStatus.Active"/> o <see cref="TenantStatus.Suspended"/>.
/// </summary>
public record ChangeTenantStatusRequest(TenantStatus Status);
