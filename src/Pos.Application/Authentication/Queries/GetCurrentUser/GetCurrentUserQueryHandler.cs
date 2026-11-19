using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Application.Authentication.Queries.GetCurrentUser;

/// <summary>
/// Handler de <c>GetCurrentUserQuery</c>.
/// Resuelve la identidad únicamente de los claims del JWT (sin consulta a BD),
/// con invariantes fail-closed de ADR 0023.
/// </summary>
public class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private readonly ICurrentUserClaims _currentUserClaims;

    public GetCurrentUserQueryHandler(ICurrentUserClaims currentUserClaims)
    {
        _currentUserClaims = currentUserClaims ?? throw new ArgumentNullException(nameof(currentUserClaims));
    }

    public Task<CurrentUserResponse> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken = default)
    {
        if (!_currentUserClaims.IsAuthenticated ||
            !_currentUserClaims.UserId.HasValue ||
            !_currentUserClaims.RoleId.HasValue ||
            string.IsNullOrWhiteSpace(_currentUserClaims.Role) ||
            string.IsNullOrWhiteSpace(_currentUserClaims.Email) ||
            string.IsNullOrWhiteSpace(_currentUserClaims.FullName))
        {
            throw new UnauthorizedDomainException(
                "La identidad del usuario autenticada es incompleta o inconsistente.");
        }

        return Task.FromResult(new CurrentUserResponse(
            _currentUserClaims.UserId.Value,
            _currentUserClaims.RoleId.Value,
            _currentUserClaims.Role,
            _currentUserClaims.Email,
            _currentUserClaims.FullName,
            _currentUserClaims.TenantId));
    }
}
