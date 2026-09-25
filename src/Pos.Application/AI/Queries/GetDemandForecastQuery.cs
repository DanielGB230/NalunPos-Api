using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.AI.Abstractions;
using Pos.Application.AI.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Queries;

[HasPermission(Permissions.AiGovernance.View)]
public record GetDemandForecastQuery(Guid ProductId, int DaysAhead = 30) : IQuery<Result<DemandForecastDto>>;

public class GetDemandForecastQueryHandler : IQueryHandler<GetDemandForecastQuery, Result<DemandForecastDto>>
{
    private readonly IDemandForecastCapability _forecastCapability;
    private readonly IProductRepository _productRepository;

    public GetDemandForecastQueryHandler(
        IDemandForecastCapability forecastCapability,
        IProductRepository productRepository)
    {
        _forecastCapability = forecastCapability ?? throw new ArgumentNullException(nameof(forecastCapability));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result<DemandForecastDto>> HandleAsync(GetDemandForecastQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Fail<DemandForecastDto>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con el ID '{request.ProductId}'."));
        }

        int predictedQuantity = await _forecastCapability.PredictRequiredStockAsync(
            request.ProductId,
            request.DaysAhead,
            cancellationToken);

        var dto = new DemandForecastDto(
            request.ProductId,
            request.DaysAhead,
            predictedQuantity,
            DateTime.UtcNow
        );

        return Result.Ok(dto);
    }
}
