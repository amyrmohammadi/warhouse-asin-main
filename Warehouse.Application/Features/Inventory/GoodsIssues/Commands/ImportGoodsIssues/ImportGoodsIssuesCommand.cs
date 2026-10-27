using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;

public sealed record ImportGoodsIssuesCommand(string FileName, byte[] FileContent)
    : IRequest<Result<GoodsIssueImportSummary>>
{
    public const int MaxFileSizeBytes = 50 * 1024 * 1024;
}

public sealed record GoodsIssueImportRowError(string Sheet, int RowNumber, string Message);

public sealed class GoodsIssueImportSummary
{
    public int ProcessedRowCount { get; init; }
    public int ImportedIssueCount { get; init; }
    public int ImportedItemCount { get; init; }
    public int CreatedIssuerCount { get; init; }
    public IReadOnlyList<GoodsIssueImportRowError> InvalidRows { get; init; } = [];
}
