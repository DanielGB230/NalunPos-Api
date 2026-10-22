namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

public sealed class FakeCurrentUserService : ICurrentUserService
{
    /// <summary>Identificador del usuario actual configurable para pruebas.</summary>
    public Guid? UserId { get; set; }
    /// <summary>Correo electrónico simulado del usuario actual.</summary>
    public string? UserEmail => "test@example.com";
    public bool IsSystemContext { get; set; }
}
