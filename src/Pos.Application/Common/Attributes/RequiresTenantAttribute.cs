namespace Pos.Application.Common.Attributes;

/// <summary>
/// Atributo marcador que indica que un comando requiere un contexto de tenant activo válido (fail-closed).
/// Debe declararse exclusivamente en el record o clase del comando (ICommand), nunca en el handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class RequiresTenantAttribute : Attribute
{
}
