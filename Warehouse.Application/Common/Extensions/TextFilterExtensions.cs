using System.Linq.Expressions;
using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Extensions;

public static class TextFilterExtensions
{
    public static IQueryable<TEntity> ApplyTextFilter<TEntity>(
        this IQueryable<TEntity> query,
        TextFilter? filter,
        Expression<Func<TEntity, string?>> selector)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        if (filter is null) return query;

        var body = BuildPredicate(selector.Body, filter);
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    public static IQueryable<TEntity> ApplyAnyTextFilter<TEntity, TChild>(
        this IQueryable<TEntity> query,
        TextFilter? filter,
        Expression<Func<TEntity, IEnumerable<TChild>>> collectionSelector,
        Expression<Func<TChild, string?>> selector)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(selector);

        if (filter is null) return query;

        var childBody = BuildPredicate(selector.Body, filter);
        var childPredicate = Expression.Lambda<Func<TChild, bool>>(
            childBody,
            selector.Parameters);
        var anyCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Any),
            [typeof(TChild)],
            collectionSelector.Body,
            childPredicate);

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(
            anyCall,
            collectionSelector.Parameters));
    }

    private static Expression BuildPredicate(Expression member, TextFilter filter)
    {
        var terms = filter.GetTerms();
        if (terms.Length == 0)
        {
            throw new ArgumentException(
                "At least one value is required for an active text filter.",
                nameof(filter));
        }

        Expression? body = null;
        foreach (var term in terms)
        {
            var value = Expression.Constant(term);
            Expression comparison = filter.Operator switch
            {
                TextOperator.Contains => Expression.Call(
                    member,
                    nameof(string.Contains),
                    Type.EmptyTypes,
                    value),
                TextOperator.Eq => Expression.Equal(member, value),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(filter),
                    filter.Operator,
                    "The text operator is not supported.")
            };
            var termBody = Expression.AndAlso(
                Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                comparison);
            body = body is null ? termBody : Expression.OrElse(body, termBody);
        }

        return body!;
    }
}
