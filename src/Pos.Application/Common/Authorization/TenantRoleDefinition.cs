namespace Pos.Application.Common.Authorization;

/// <summary>
/// Definición inmutable de un rol de sistema perteneciente al catálogo predeterminado de cada tenant.
/// </summary>
public sealed record TenantRoleDefinition(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);

