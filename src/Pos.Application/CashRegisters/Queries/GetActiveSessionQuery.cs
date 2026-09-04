using Pos.Application.Common.Interfaces;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.CashRegisters.Queries;

public record GetActiveSessionQuery(Guid CashRegisterId) : IQuery<CashRegisterSessionDto?>;

public class GetActiveSessionQueryHandler : IQueryHandler<GetActiveSessionQuery, CashRegisterSessionDto?>
{
    private readonly ICashRegisterRepository _registerRepository;

    public GetActiveSessionQueryHandler(ICashRegisterRepository registerRepository)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
    }

    public async Task<CashRegisterSessionDto?> HandleAsync(GetActiveSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _registerRepository.GetActiveSessionByRegisterIdAsync(request.CashRegisterId, cancellationToken);
        return session != null ? CashRegisterSessionDto.FromEntity(session) : null;
    }
}
