namespace Warehouse.Application.Common.Models.Filtering;

public abstract record DataTableParameters
{
    public bool PaginationEnabled { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Search { get; init; }

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}
