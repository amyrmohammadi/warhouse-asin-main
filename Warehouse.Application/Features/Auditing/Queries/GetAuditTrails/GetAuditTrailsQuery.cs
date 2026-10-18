using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Auditing;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

public sealed record GetAuditTrailsQuery : DataTableParameters, IRequest<Result<GetAuditTrailsResponse>>
{
    public GetAuditTrailsQuery()
    {
        SortDescending = true;
    }

    public string? UserFullName { get; init; }

    public string? Action { get; init; }

    public string? TableName { get; init; }

    public string? PrimaryKey { get; init; }

    public string? ChangedColumns { get; init; }
}

internal sealed class GetAuditTrailsQueryHandler(
    IAuditTrailService auditTrailService,
    IMapper mapper) : IRequestHandler<GetAuditTrailsQuery, Result<GetAuditTrailsResponse>>
{
    public async Task<Result<GetAuditTrailsResponse>> Handle(
        GetAuditTrailsQuery request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<GetAuditTrailsRequest>(request);
        var auditTrails = await auditTrailService.GetAuditTrailsAsync(
            serviceRequest,
            cancellationToken);

        return Result<GetAuditTrailsResponse>.Success(
            mapper.Map<GetAuditTrailsResponse>(auditTrails));
    }
}
