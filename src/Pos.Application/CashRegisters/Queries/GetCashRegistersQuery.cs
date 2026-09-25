using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.CashRegisters.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.CashRegisters.Queries;

[HasPermission(Permissions.CashRegisters.View)]
public record GetCashRegistersQuery : IQuery<IReadOnlyList<CashRegisterDto>>;

[HasPermission(Permissions.CashRegisters.View)]
public class GetCashRegistersQueryHandler : IQueryHandler<GetCashRegistersQuery, IReadOnlyList<CashRegisterDto>>
{
    private readonly ICashRegisterRepository _registerRepository;

    public GetCashRegistersQueryHandler(ICashRegisterRepository registerRepository)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
    }

    public async Task<IReadOnlyList<CashRegisterDto>> HandleAsync(GetCashRegistersQuery request, CancellationToken cancellationToken)
    {
        var items = await _registerRepository.GetAllAsync(cancellationToken);
        return items.Select(CashRegisterDto.FromEntity).ToList();
    }
}
