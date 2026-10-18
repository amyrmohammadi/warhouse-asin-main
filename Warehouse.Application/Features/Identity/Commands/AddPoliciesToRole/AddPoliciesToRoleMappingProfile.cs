using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;

public sealed class AddPoliciesToRoleMappingProfile : Profile
{
    public AddPoliciesToRoleMappingProfile()
    {
        CreateMap<AddPoliciesToRoleCommand, AddPoliciesToRoleRequest>();
    }
}
