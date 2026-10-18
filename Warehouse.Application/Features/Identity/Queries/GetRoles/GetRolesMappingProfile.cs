using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Queries.GetRoles;

public sealed class GetRolesMappingProfile : Profile
{
    public GetRolesMappingProfile()
    {
        CreateMap<GetRolesQuery, GetRolesRequest>();
        CreateMap<RoleSummary, RoleListItemResponse>();
        CreateMap<GetRolesResult, GetRolesResponse>();
    }
}
