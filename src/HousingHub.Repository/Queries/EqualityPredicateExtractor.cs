using System.Linq.Expressions;

namespace HousingHub.Repository.Queries;

/// <summary>
/// Pulls indexable equality comparisons out of a filter expression.
/// </summary>
/// <remarks>
/// <para>
/// The repository takes arbitrary <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>
/// predicates and, historically, satisfied every one of them by scanning the whole
/// table and filtering in memory. Almost all of those predicates are — or contain —
/// a simple equality against a key or an indexed column, which DynamoDB can answer
/// directly.
/// </para>
/// <para>
/// This walks the expression and returns the equality comparisons that could narrow
/// the read. It is deliberately conservative: only AND-ed comparisons of a property
/// against a value are collected. An OR anywhere means no single index can be
/// trusted to return every matching row, so nothing is returned from that branch and
/// the caller falls back to a scan.
/// </para>
/// <para>
/// Correctness does not depend on this being clever. The caller always re-applies
/// the original predicate to whatever comes back, so the index only ever shrinks the
/// candidate set — a missed opportunity costs performance, never correctness.
/// </para>
/// </remarks>
internal static class EqualityPredicateExtractor
{
    internal readonly record struct Candidate(string PropertyName, object? Value);

    public static IReadOnlyList<Candidate> Extract<T>(Expression<Func<T, bool>>? predicate)
    {
        if (predicate is null) return [];

        var found = new List<Candidate>();
        Walk(predicate.Body, predicate.Parameters[0], found);
        return found;
    }

    private static void Walk(Expression node, ParameterExpression parameter, List<Candidate> found)
    {
        switch (node)
        {
            // Both sides of an AND must hold, so an equality in either side is a
            // valid way to narrow the read.
            case BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.And } conjunction:
                Walk(conjunction.Left, parameter, found);
                Walk(conjunction.Right, parameter, found);
                return;

            case BinaryExpression { NodeType: ExpressionType.Equal } equality:
                if (TryReadEquality(equality, parameter, out var candidate))
                    found.Add(candidate);
                return;

            // Strip away conversions and parentheses the compiler inserts.
            case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.Quote } unary:
                Walk(unary.Operand, parameter, found);
                return;

            // OrElse, Not, method calls, anything else: not safely indexable.
            default:
                return;
        }
    }

    private static bool TryReadEquality(
        BinaryExpression equality,
        ParameterExpression parameter,
        out Candidate candidate)
    {
        candidate = default;

        // Accept either operand order: x.Prop == value, or value == x.Prop.
        if (TryReadPropertyName(equality.Left, parameter, out var name)
            && TryEvaluate(equality.Right, out var right))
        {
            candidate = new Candidate(name, right);
            return true;
        }

        if (TryReadPropertyName(equality.Right, parameter, out name)
            && TryEvaluate(equality.Left, out var left))
        {
            candidate = new Candidate(name, left);
            return true;
        }

        return false;
    }

    /// <summary>Matches a direct member access on the lambda parameter, e.g. <c>x.Email</c>.</summary>
    private static bool TryReadPropertyName(
        Expression expression,
        ParameterExpression parameter,
        out string propertyName)
    {
        propertyName = string.Empty;

        // Unwrap the Convert the compiler adds around enums and nullables.
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            expression = unary.Operand;

        if (expression is not MemberExpression member) return false;
        if (member.Expression != parameter) return false;

        propertyName = member.Member.Name;
        return true;
    }

    /// <summary>
    /// Evaluates the non-property side. Usually a captured local, so it is a closure
    /// field access rather than a literal and has to be invoked to get the value.
    /// </summary>
    private static bool TryEvaluate(Expression expression, out object? value)
    {
        value = null;

        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            expression = unary.Operand;

        if (expression is ConstantExpression constant)
        {
            value = constant.Value;
            return true;
        }

        try
        {
            value = Expression.Lambda(expression).Compile().DynamicInvoke();
            return true;
        }
        catch
        {
            // Anything that cannot be evaluated standalone — a call depending on the
            // row, say — simply is not a usable key. Fall back to scanning.
            return false;
        }
    }
}
