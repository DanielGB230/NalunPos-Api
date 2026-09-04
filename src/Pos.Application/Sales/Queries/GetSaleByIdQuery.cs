using Pos.Application.Common.Interfaces;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Sales.Queries;

public record GetSaleByIdQuery(Guid Id) : IQuery<SaleDto>;

public class GetSaleByIdQueryHandler : IQueryHandler<GetSaleByIdQuery, SaleDto>
{
    private readonly ISaleRepository _saleRepository;

    public GetSaleByIdQueryHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
    }

    public async Task<SaleDto> HandleAsync(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var sale = await _saleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new SaleNotFoundException(request.Id);

        return SaleDto.FromEntity(sale);
    }
}
