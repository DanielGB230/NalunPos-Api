namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Puerto de infraestructura encargado de administrar el alcance de conexión y
/// <c>SESSION_CONTEXT</c> durante las consultas de <see cref="AuthUserLookup"/>.
/// </summary>
public interface IAuthenticationSessionScope
{
    Task<IAuthenticationSession> BeginAsync(CancellationToken cancellationToken = default);
}

