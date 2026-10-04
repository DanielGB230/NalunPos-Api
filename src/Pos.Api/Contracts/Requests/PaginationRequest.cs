namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato base de paginación HTTP.
/// DTO simple de paginación. La única autoridad del límite de tamaño de página es Pos.Application.Common.Validation.PaginationRules.
/// </summary>
public record PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
