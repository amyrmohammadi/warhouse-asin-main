using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.CreateUser;

public sealed class CreateUserMappingProfile : Profile
{
    public CreateUserMappingProfile()
    {
        CreateMap<CreateUserCommand, CreateUserRequest>();
        CreateMap<CreateUserResult, CreateUserResponse>();
    }
}
