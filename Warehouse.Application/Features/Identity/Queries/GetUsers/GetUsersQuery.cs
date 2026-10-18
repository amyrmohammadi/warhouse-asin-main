using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Queries.GetUsers;

public sealed record GetUsersQuery : DataTableParameters, IRequest<Result<GetUsersResponse>>
{
    public string? FullName { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public bool? IsActive { get; init; }

    public string? RoleName { get; init; }
}

internal sealed class GetUsersQueryHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<GetUsersQuery, Result<GetUsersResponse>>
{
    public async Task<Result<GetUsersResponse>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<GetUsersRequest>(request);
        var users = await identityService.GetUsersAsync(serviceRequest, cancellationToken);

        return Result<GetUsersResponse>.Success(
            mapper.Map<GetUsersResponse>(users));
    }
}
