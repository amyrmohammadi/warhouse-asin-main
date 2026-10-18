using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;

public sealed record ImportSuppliersCommand(
    string FileName,
    byte[] FileContent) : IRequest<Result<SupplierImportSummary>>
{
    public const int MaxFileSizeBytes = 10 * 1024 * 1024;
}
