using System.Linq.Expressions;

namespace Scadex.Core.Utils.DynamicQuery;

/// <summary> Provides extension methods for combining LINQ expressions. </summary>
public static class ExpressionExtensions
{
    /// <summary> Combines two LINQ predicates using a logical AND operation. If <paramref name="right"/> is null, <paramref name="left"/> is returned unchanged. </summary>
    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>>? right)
    {
        if (right is null)
            return left;

        var parameter = Expression.Parameter(typeof(T), "x");

        var leftBody = new ParameterRebinder(parameter).Visit(left.Body);
        var rightBody = new ParameterRebinder(parameter).Visit(right.Body);

        var body = Expression.AndAlso(leftBody, rightBody);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary> Replaces expression parameters with a shared parameter. </summary>
    private sealed class ParameterRebinder(ParameterExpression parameter) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => parameter;
    }
}