using AutoMapper;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenMappingProfile : Profile
{
    public RefreshTokenMappingProfile()
    {
        CreateMap<RefreshTokenCommand, RefreshTokenRequest>();
        CreateMap<TokenResult, RefreshTokenResponse>();
    }
}
