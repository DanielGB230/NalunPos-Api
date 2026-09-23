using Pos.Application.Common.Models;

namespace Pos.Application.StockTransfers.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de traslados de stock.
/// El default de PageSize es 20 en lugar de 10 para este dominio.
/// </summary>
public record GetStockTransfersRequest(
    int PageNumber = 1,
    int PageSize = 20
) : PaginationRequest(PageNumber, PageSize);

