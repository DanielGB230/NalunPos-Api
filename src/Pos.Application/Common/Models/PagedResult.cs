namespace Pos.Application.Common.Models;

/// <summary>
/// Modelo genérico de resultado paginado para consultas de listado.
/// </summary>
/// <typeparam name="T">Tipo del elemento paginado.</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items ?? Array.Empty<T>();
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = pageSize < 1 ? 10 : pageSize;
        TotalCount = totalCount < 0 ? 0 : totalCount;
    }
}
