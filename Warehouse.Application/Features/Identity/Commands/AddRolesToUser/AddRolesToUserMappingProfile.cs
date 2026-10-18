using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.AddRolesToUser;

public sealed class AddRolesToUserMappingProfile : Profile
{
    public AddRolesToUserMappingProfile()
    {
        CreateMap<AddRolesToUserCommand, AddRolesToUserRequest>();
    }
}
