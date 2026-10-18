using FluentValidation;

namespace Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

internal sealed class GetAuditTrailsQueryValidator : AbstractValidator<GetAuditTrailsQuery>
{
    private static readonly string[] SupportedSortFields =
    [
        "id",
        "userFullName",
        "action",
        "tableName",
        "occurredAtUtc"
    ];

    public GetAuditTrailsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.Search));
        RuleFor(query => query.UserFullName)
            .MaximumLength(200)
            .When(query => !string.IsNullOrWhiteSpace(query.UserFullName));
        RuleFor(query => query.Action)
            .Must(action => new[] { "Create", "Update", "Delete" }.Contains(
                action!,
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("The selected audit action is not supported.")
            .When(query => !string.IsNullOrWhiteSpace(query.Action));
        RuleFor(query => query.TableName)
            .MaximumLength(128)
            .When(query => !string.IsNullOrWhiteSpace(query.TableName));
        RuleFor(query => query.PrimaryKey)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.PrimaryKey));
        RuleFor(query => query.ChangedColumns)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.ChangedColumns));
        RuleFor(query => query.SortBy)
            .Must(sortBy => SupportedSortFields.Contains(
                sortBy!,
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("The selected audit log sort field is not supported.")
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
    }
}
