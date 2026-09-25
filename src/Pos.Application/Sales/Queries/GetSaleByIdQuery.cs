using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Sales.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Sales.Queries;

[HasPermission(Permissions.Sales.View)]
public record GetSaleByIdQuery(Guid Id) : IQuery<Result<SaleDto>>;

[HasPermission(Permissions.Sales.View)]
public class GetSaleByIdQueryHandler : IQueryHandler<GetSaleByIdQuery, Result<SaleDto>>
{
    private readonly ISaleRepository _saleRepository;

    public GetSaleByIdQueryHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
    }

    public async Task<Result<SaleDto>> HandleAsync(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sale = await _saleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sale == null)
        {
            return Result.Fail<SaleDto>(DomainError.NotFound(
                "Sale.NotFound",
                $"No se encontró la venta con ID '{request.Id}'."));
        }

        return Result.Ok(SaleDto.FromEntity(sale));
    }
}
