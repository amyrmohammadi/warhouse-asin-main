using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;

public sealed record AddPoliciesToRoleCommand(
    string RoleName,
    IReadOnlyCollection<string> Policies) : IRequest<Result>;

internal sealed class AddPoliciesToRoleCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<AddPoliciesToRoleCommand, Result>
{
    public async Task<Result> Handle(
        AddPoliciesToRoleCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<AddPoliciesToRoleRequest>(request);
        var result = await identityService.AddPoliciesToRoleAsync(
            serviceRequest,
            cancellationToken);

        return result.Succeeded
            ? Result.Success()
            : Result.Error(result.Errors);
    }
}
