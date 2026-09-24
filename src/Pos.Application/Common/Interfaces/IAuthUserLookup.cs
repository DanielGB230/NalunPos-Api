using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto explícito de plataforma para la búsqueda de usuarios por email durante la autenticación (login).
/// Es la ÚNICA consulta cross-tenant legítima autorizada en la solución (ADR 0008).
/// </summary>
public interface IAuthUserLookup
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
