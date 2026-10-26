using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid Id) : IRequest<Result<InvoiceDetails>>;

internal sealed class GetInvoiceByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDetails>>
{
    public async Task<Result<InvoiceDetails>> Handle(
        GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Invoice>();
        var invoice = (await repository.ToListAsync(
            repository.Entities
                .Where(item => item.Id == request.Id)
                .ToDetails(),
            cancellationToken)).FirstOrDefault();

        return invoice is null
            ? Result<InvoiceDetails>.Error("فاکتور پیدا نشد.")
            : Result<InvoiceDetails>.Success(invoice);
    }
}
