using Pos.Application.Common.Interfaces;
using Pos.Application.AI.Abstractions;
using Pos.Application.AI.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Queries;

public record GetDemandForecastQuery(Guid ProductId, int DaysAhead = 30) : IQuery<DemandForecastDto>;

public class GetDemandForecastQueryHandler : IQueryHandler<GetDemandForecastQuery, DemandForecastDto>
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

    public async Task<DemandForecastDto> HandleAsync(GetDemandForecastQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(request.ProductId);

        int predictedQuantity = await _forecastCapability.PredictRequiredStockAsync(
            request.ProductId,
            request.DaysAhead,
            cancellationToken);

        return new DemandForecastDto(
            request.ProductId,
            request.DaysAhead,
            predictedQuantity,
            DateTime.UtcNow
        );
    }
}
