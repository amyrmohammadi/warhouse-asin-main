using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.Login;

public sealed class LoginMappingProfile : Profile
{
    public LoginMappingProfile()
    {
        CreateMap<LoginCommand, LoginRequest>();
        CreateMap<TokenResult, LoginResponse>();
    }
}
