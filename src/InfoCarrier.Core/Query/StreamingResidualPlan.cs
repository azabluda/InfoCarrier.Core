// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Applies row-local client operators without collecting their source first.
/// </summary>
internal sealed class StreamingResidualPlan
{
    private readonly IReadOnlyList<Step> _steps;

    private StreamingResidualPlan(IReadOnlyList<Step> steps)
        => _steps = steps;

    internal static bool TryCreate(LambdaExpression residual, out StreamingResidualPlan? plan)
    {
        plan = null;
        if (residual.Parameters.Count != 1)
        {
            return false;
        }

        var steps = new List<Step>();
        if (!TryRead(residual.Body, residual.Parameters[0], steps))
        {
            return false;
        }

        plan = new StreamingResidualPlan(steps);
        return true;
    }

    internal StreamingResidualResult Process(object? value)
    {
        foreach (Step step in _steps)
        {
            StreamingResidualResult result = step.Process(value);
            if (result.Kind != StreamingResidualResultKind.Yield)
            {
                return result;
            }

            value = result.Value;
        }

        return StreamingResidualResult.Yield(value);
    }

    private static bool TryRead(Expression expression, ParameterExpression source, List<Step> steps)
    {
        if (expression == source)
        {
            return true;
        }

        if (expression is not MethodCallExpression call
            || call.Method.DeclaringType != typeof(Queryable)
            || call.Arguments.Count == 0
            || !TryRead(call.Arguments[0], source, steps))
        {
            return false;
        }

        Step? step = call.Method.Name switch
        {
            nameof(Queryable.Where) when call.Arguments.Count == 2
                => new PredicateStep(CompileLambda(call.Arguments[1])),
            nameof(Queryable.Select) when call.Arguments.Count == 2
                => new SelectorStep(CompileLambda(call.Arguments[1])),
            nameof(Queryable.Skip) when call.Arguments.Count == 2
                => new SkipStep(EvaluateCount(call.Arguments[1])),
            nameof(Queryable.Take) when call.Arguments.Count == 2
                => new TakeStep(EvaluateCount(call.Arguments[1])),
            nameof(Queryable.Distinct) when call.Arguments.Count == 1
                => new DistinctStep(),
            _ => null,
        };

        if (step is null)
        {
            return false;
        }

        steps.Add(step);
        return true;
    }

    private static Delegate CompileLambda(Expression expression)
        => ((LambdaExpression)StripQuote(expression)).Compile();

    private static int EvaluateCount(Expression expression)
        => Expression.Lambda<Func<int>>(expression).Compile()();

    private static Expression StripQuote(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            expression = quote.Operand;
        }

        return expression;
    }

    private abstract class Step
    {
        internal abstract StreamingResidualResult Process(object? value);
    }

    private sealed class PredicateStep(Delegate predicate) : Step
    {
        private int _index;

        internal override StreamingResidualResult Process(object? value)
        {
            object? accepted = predicate.GetType().GetMethod("Invoke")!.GetParameters().Length == 2
                ? predicate.DynamicInvoke(value, _index++)
                : predicate.DynamicInvoke(value);
            return accepted is true
                ? StreamingResidualResult.Yield(value)
                : StreamingResidualResult.Skip;
        }
    }

    private sealed class SelectorStep(Delegate selector) : Step
    {
        private int _index;

        internal override StreamingResidualResult Process(object? value)
            => StreamingResidualResult.Yield(
                selector.GetType().GetMethod("Invoke")!.GetParameters().Length == 2
                    ? selector.DynamicInvoke(value, _index++)
                    : selector.DynamicInvoke(value));
    }

    private sealed class SkipStep(int count) : Step
    {
        private int _remaining = Math.Max(count, 0);

        internal override StreamingResidualResult Process(object? value)
        {
            if (_remaining == 0)
            {
                return StreamingResidualResult.Yield(value);
            }

            _remaining--;
            return StreamingResidualResult.Skip;
        }
    }

    private sealed class TakeStep(int count) : Step
    {
        private int _remaining = Math.Max(count, 0);

        internal override StreamingResidualResult Process(object? value)
        {
            if (_remaining == 0)
            {
                return StreamingResidualResult.Complete;
            }

            _remaining--;
            return StreamingResidualResult.Yield(value);
        }
    }

    private sealed class DistinctStep : Step
    {
        private readonly HashSet<object?> _seen = [];

        internal override StreamingResidualResult Process(object? value)
            => _seen.Add(value)
                ? StreamingResidualResult.Yield(value)
                : StreamingResidualResult.Skip;
    }
}

internal enum StreamingResidualResultKind
{
    Yield,
    Skip,
    Complete,
}

internal readonly record struct StreamingResidualResult(
    StreamingResidualResultKind Kind,
    object? Value)
{
    internal static StreamingResidualResult Yield(object? value)
        => new(StreamingResidualResultKind.Yield, value);

    internal static StreamingResidualResult Skip
        => new(StreamingResidualResultKind.Skip, null);

    internal static StreamingResidualResult Complete
        => new(StreamingResidualResultKind.Complete, null);
}

/// <summary>
///     Describes a concatenation whose result slots can be drained in order.
/// </summary>
internal sealed class StreamingConcatPlan
{
    private StreamingConcatPlan(IReadOnlyList<int> slots)
        => Slots = slots;

    internal IReadOnlyList<int> Slots { get; }

    internal static bool TryCreate(LambdaExpression residual, out StreamingConcatPlan? plan)
    {
        var slots = new List<int>();
        if (!TryRead(residual.Body, residual.Parameters, slots)
            || slots.Count != residual.Parameters.Count
            || slots.Distinct().Count() != slots.Count)
        {
            plan = null;
            return false;
        }

        plan = new StreamingConcatPlan(slots);
        return true;
    }

    private static bool TryRead(
        Expression expression,
        IReadOnlyList<ParameterExpression> parameters,
        List<int> slots)
    {
        if (expression is ParameterExpression parameter)
        {
            int slot = parameters.IndexOf(parameter);
            if (slot < 0)
            {
                return false;
            }

            slots.Add(slot);
            return true;
        }

        return expression is MethodCallExpression
            {
                Method.Name: nameof(Queryable.Concat),
                Method.DeclaringType: { } declaring,
                Arguments.Count: 2,
            } call
            && declaring == typeof(Queryable)
            && TryRead(call.Arguments[0], parameters, slots)
            && TryRead(call.Arguments[1], parameters, slots);
    }
}

internal static class ParameterListExtensions
{
    internal static int IndexOf(
        this IReadOnlyList<ParameterExpression> parameters,
        ParameterExpression candidate)
    {
        for (int index = 0; index < parameters.Count; index++)
        {
            if (ReferenceEquals(parameters[index], candidate))
            {
                return index;
            }
        }

        return -1;
    }
}
