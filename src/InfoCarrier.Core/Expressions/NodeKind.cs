// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     The kind of an <see cref="ExpressionNode" />. Explicit enum map across the
///     System↔remote boundary — never an int-cast ABI (expression-serialization §3.7).
///     Minimal set per research-findings §5: only nodes LINQ-to-EF produces.
/// </summary>
public enum NodeKind
{
    /// <summary>A constant value.</summary>
    Constant = 0,

    /// <summary>A lambda/closure parameter.</summary>
    Parameter = 1,

    /// <summary>Member (property/field) access.</summary>
    Member = 2,

    /// <summary>A method call.</summary>
    MethodCall = 3,

    /// <summary>A lambda expression.</summary>
    Lambda = 4,

    /// <summary>An object construction (new).</summary>
    New = 5,

    /// <summary>An array construction (new[]).</summary>
    NewArray = 6,

    /// <summary>A binary operator (add, equal, and-also, …).</summary>
    Binary = 7,

    /// <summary>A unary operator (not, negate, convert, …).</summary>
    Unary = 8,

    /// <summary>A type test / type-as (TypeIs / TypeAs).</summary>
    TypeBinary = 9,

    /// <summary>A conditional (ternary) expression.</summary>
    Conditional = 10,

    /// <summary>A member-init expression (new T { … }).</summary>
    MemberInit = 11,

    /// <summary>A list-init expression (new List&lt;T&gt; { … }).</summary>
    ListInit = 12,

    /// <summary>An invocation of a lambda/delegate.</summary>
    Invocation = 13,

    /// <summary>
    ///     A query-root stub standing in for EF Core's <c>EntityQueryRootExpression</c>
    ///     (research-findings §2). Carries entity-type identity; rebound server-side.
    /// </summary>
    QueryRootStub = 14,
}
