namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Representa la ausencia de un valor de retorno en Commands sin resultado explícito.
/// Equivalente a MediatR.Unit — definido aquí para eliminar cualquier dependencia externa.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>Instancia singleton de Unit.</summary>
    public static readonly Unit Value;

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";
    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
