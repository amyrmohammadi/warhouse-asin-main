using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Queries.GetUsers;

public sealed class GetUsersMappingProfile : Profile
{
    public GetUsersMappingProfile()
    {
        CreateMap<GetUsersQuery, GetUsersRequest>();
        CreateMap<UserSummary, UserListItemResponse>();
        CreateMap<GetUsersResult, GetUsersResponse>();
    }
}
