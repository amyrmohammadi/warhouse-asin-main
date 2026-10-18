using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;

public sealed record ImportProductsCommand(
    string FileName,
    byte[] FileContent) : IRequest<Result<ProductImportSummary>>
{
    public const int MaxFileSizeBytes = 50 * 1024 * 1024;
}
