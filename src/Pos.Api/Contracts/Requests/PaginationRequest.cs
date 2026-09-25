namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato base de paginación HTTP.
/// Enforma de manera consistente un tope máximo de PageSize (100).
/// </summary>
public record PaginationRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}
