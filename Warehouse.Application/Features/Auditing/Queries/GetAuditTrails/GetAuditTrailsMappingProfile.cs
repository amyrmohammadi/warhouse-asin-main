using AutoMapper;
using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

public sealed class GetAuditTrailsMappingProfile : Profile
{
    public GetAuditTrailsMappingProfile()
    {
        CreateMap<GetAuditTrailsQuery, GetAuditTrailsRequest>();
        CreateMap<AuditTrailSummary, AuditTrailListItemResponse>();
        CreateMap<GetAuditTrailsResult, GetAuditTrailsResponse>();
    }
}
