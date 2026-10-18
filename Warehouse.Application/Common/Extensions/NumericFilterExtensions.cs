using System.Linq.Expressions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Extensions;

public static class NumericFilterExtensions
{
    public static IQueryable<TEntity> ApplyNumericFilter<TEntity, TNumber>(
        this IQueryable<TEntity> query,
        NumericFilter<TNumber>? filter,
        Expression<Func<TEntity, TNumber>> selector)
        where TNumber : struct, IComparable<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        if (filter is null)
        {
            return query;
        }

        var value = filter.Value ?? throw new ArgumentException(
            "A value is required for an active numeric filter.",
            nameof(filter));
        var valueExpression = Expression.Constant(value, typeof(TNumber));
        var valueToExpression = filter.ValueTo is { } valueTo
            ? Expression.Constant(valueTo, typeof(TNumber))
            : null;
        var body = BuildComparison(
            selector.Body,
            valueExpression,
            valueToExpression,
            filter.Operator);

        return query.Where(
            Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    public static IQueryable<TEntity> ApplyNumericFilter<TEntity, TNumber>(
        this IQueryable<TEntity> query,
        NumericFilter<TNumber>? filter,
        Expression<Func<TEntity, TNumber?>> selector)
        where TNumber : struct, IComparable<TNumber>
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        if (filter is null)
        {
            return query;
        }

        var value = filter.Value ?? throw new ArgumentException(
            "A value is required for an active numeric filter.",
            nameof(filter));
        var valueExpression = Expression.Convert(
            Expression.Constant(value, typeof(TNumber)),
            typeof(TNumber?));
        var valueToExpression = filter.ValueTo is { } valueTo
            ? Expression.Convert(
                Expression.Constant(valueTo, typeof(TNumber)),
                typeof(TNumber?))
            : null;
        var body = BuildComparison(
            selector.Body,
            valueExpression,
            valueToExpression,
            filter.Operator);

        return query.Where(
            Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    private static Expression BuildComparison(
        Expression member,
        Expression value,
        Expression? valueTo,
        NumericOperator numericOperator) => numericOperator switch
    {
        NumericOperator.Eq => Expression.Equal(member, value),
        NumericOperator.Neq => Expression.NotEqual(member, value),
        NumericOperator.Gt => Expression.GreaterThan(member, value),
        NumericOperator.Gte => Expression.GreaterThanOrEqual(member, value),
        NumericOperator.Lt => Expression.LessThan(member, value),
        NumericOperator.Lte => Expression.LessThanOrEqual(member, value),
        NumericOperator.Between when valueTo is not null => Expression.AndAlso(
            Expression.GreaterThanOrEqual(member, value),
            Expression.LessThanOrEqual(member, valueTo)),
        NumericOperator.Between => throw new ArgumentException(
            "The upper value is required for a between filter."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(numericOperator),
            numericOperator,
            "The numeric operator is not supported.")
    };
}
