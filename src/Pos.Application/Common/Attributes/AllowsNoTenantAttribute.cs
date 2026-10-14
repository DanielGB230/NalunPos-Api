namespace Pos.Application.Common.Attributes;

/// <summary>
/// Indica explícitamente que el comando de aplicación puede ejecutarse en ausencia de un contexto de tenant activo.
/// Se reserva estrictamente para casos de uso públicos (ej. Login) o de plataforma (ej. Creación y aprovisionamiento de Tenant).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AllowsNoTenantAttribute : Attribute
{
}
