using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Queries.GetRoles;

public sealed record GetRolesQuery : DataTableParameters, IRequest<Result<GetRolesResponse>>
{
    public string? RoleName { get; init; }

    public NumericFilter<int>? UserCount { get; init; }

    public NumericFilter<int>? PermissionCount { get; init; }
}

internal sealed class GetRolesQueryHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<GetRolesQuery, Result<GetRolesResponse>>
{
    public async Task<Result<GetRolesResponse>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<GetRolesRequest>(request);
        var roles = await identityService.GetRolesAsync(
            serviceRequest,
            cancellationToken);

        return Result<GetRolesResponse>.Success(
            mapper.Map<GetRolesResponse>(roles));
    }
}
