// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Carries a substituted query parameter's value in the shape EF's own funcletizer recognises
///     as a captured variable, so the server lifts it back into a parameter (§6, B22).
/// </summary>
/// <typeparam name="T">The parameter's declared type.</typeparam>
/// <remarks>
///     <para>
///         ADR-006 captures the tree before EF's pipeline runs, so a query parameter has to be
///         substituted with something. Research-findings §6 chose a plain
///         <c>ConstantExpression</c> of the value, and its 2026-08-02 amendment carved out
///         collections: relational EF recognises an inline collection from the <em>shape</em> of
///         the expression, so a collection is spelled out element by element as a
///         <c>NewArrayExpression</c>.
///     </para>
///     <para>
///         That shape is right for <c>IN (x, y)</c> and wrong for anything that needs a real
///         parameter — indexing a collection by a column reaches SQLite as a correlated subquery
///         whose <c>OFFSET</c> names a column out of scope, where a parameter would have been a
///         JSON string indexed with <c>-&gt;&gt;</c>. A member read over a constant is the third
///         option and the one EF is built for: <c>ExpressionTreeFuncletizer</c> treats any
///         evaluatable <c>MemberExpression</c> as a captured variable and parameterizes it, which
///         is exactly what a C# closure field looks like to it.
///     </para>
///     <para>
///         <b>This is not v1's <c>ValueWrapper&lt;T&gt;</c>, and the difference is the whole
///         reason §6 forbade wrappers.</b> v1's was a <c>private struct</c> — a type the wire
///         cannot name and the deserializer cannot construct. This is a public class with a public
///         property, admitted by <c>TypeAllowlist</c> like any other built-in generic and walked by
///         the ordinary member walk. The rule §6 states was derived from a wrapper that failed for
///         a reason this one does not have.
///     </para>
/// </remarks>
public sealed class ParameterBox<T>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterBox{T}" /> class.
    /// </summary>
    public ParameterBox()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterBox{T}" /> class.
    /// </summary>
    /// <param name="value">The parameter's value.</param>
    public ParameterBox(T value)
        => Value = value;

    /// <summary>
    ///     The parameter's value.
    /// </summary>
    public T Value { get; set; } = default!;
}
