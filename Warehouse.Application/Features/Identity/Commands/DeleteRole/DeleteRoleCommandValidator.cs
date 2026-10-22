using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.DeleteRole;

internal sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
    }
}
