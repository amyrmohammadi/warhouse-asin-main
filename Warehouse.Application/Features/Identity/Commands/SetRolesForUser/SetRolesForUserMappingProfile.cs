using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.SetRolesForUser;

public sealed class SetRolesForUserMappingProfile : Profile
{
    public SetRolesForUserMappingProfile()
    {
        CreateMap<SetRolesForUserCommand, SetRolesForUserRequest>();
    }
}
