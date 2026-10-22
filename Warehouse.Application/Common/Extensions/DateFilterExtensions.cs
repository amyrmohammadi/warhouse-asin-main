using System.Linq.Expressions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Extensions;

public static class DateFilterExtensions
{
    public static IQueryable<TEntity> ApplyDateFilter<TEntity>(
        this IQueryable<TEntity> query,
        DateFilter? filter,
        Expression<Func<TEntity, DateTimeOffset>> selector)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        if (filter is null) return query;

        var value = filter.Value ?? throw new ArgumentException(
            "A date is required for an active date filter.",
            nameof(filter));
        var startUtc = ToUtcStart(value, filter.UtcOffsetMinutes);
        var endExclusiveUtc = ToUtcStart(value.AddDays(1), filter.UtcOffsetMinutes);
        var startExpression = Expression.Constant(startUtc);
        var endExpression = Expression.Constant(endExclusiveUtc);

        var body = filter.Operator switch
        {
            DateOperator.Before => Expression.LessThan(selector.Body, endExpression),
            DateOperator.After => Expression.GreaterThanOrEqual(selector.Body, startExpression),
            DateOperator.Eq => BuildRange(selector.Body, startExpression, endExpression),
            DateOperator.Between => BuildRange(
                selector.Body,
                startExpression,
                Expression.Constant(ToUtcStart(
                    (filter.ValueTo ?? throw new ArgumentException(
                        "The end date is required for a between filter.",
                        nameof(filter))).AddDays(1),
                    filter.UtcOffsetMinutes))),
            _ => throw new ArgumentOutOfRangeException(
                nameof(filter),
                filter.Operator,
                "The date operator is not supported.")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    private static DateTimeOffset ToUtcStart(DateOnly value, int utcOffsetMinutes)
    {
        var localStart = value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(localStart, TimeSpan.FromMinutes(utcOffsetMinutes))
            .ToUniversalTime();
    }

    private static Expression BuildRange(
        Expression member,
        Expression start,
        Expression endExclusive) =>
        Expression.AndAlso(
            Expression.GreaterThanOrEqual(member, start),
            Expression.LessThan(member, endExclusive));
}
