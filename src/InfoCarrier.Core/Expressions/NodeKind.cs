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

    /// <summary>
    ///     A query-root stub carrying raw SQL, standing in for EF Core's relational
    ///     <c>FromSqlQueryRootExpression</c> (#60). A <see cref="QueryRootStub" /> that also
    ///     carries the caller's SQL text and its arguments; rebound server-side, and only on a
    ///     server that registered
    ///     <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />.
    /// </summary>
    FromSqlQueryRootStub = 15,

    /// <summary>
    ///     A query-root stub carrying raw SQL whose result is a <em>scalar</em>, standing in for
    ///     EF Core's relational <c>SqlQueryRootExpression</c> (#56). The sibling of
    ///     <see cref="FromSqlQueryRootStub" />, and separate from it because a scalar root has no
    ///     entity type for the server to resolve. Same grant, same default refusal.
    /// </summary>
    SqlQueryRootStub = 16,

    /// <summary>
    ///     The receiver of a user-defined function mapped as an INSTANCE method on the context
    ///     (<c>HasDbFunction</c> over a non-static method). Rebound server-side to the SERVER's
    ///     context.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The one thing a client context legitimately means on the far side.</b> Such a
    ///         call funcletizes to a receiver holding the live client <c>DbContext</c>, which no
    ///         wire carries and which this provider refuses everywhere else: it is an object graph
    ///         with a change tracker and a service provider, and the server has one of its own. But
    ///         in this position the receiver is not data. It says "the context", and on the server
    ///         "the context" is the server's.
    ///     </para>
    ///     <para>
    ///         <b>Only in this position.</b> A context reaching the boundary any other way is still
    ///         refused, by <c>ServerBoundaryAnalyzer.CarriesTheClientsContext</c>. The rewrite that
    ///         produces this node runs before the boundary is drawn and matches only a receiver
    ///         whose method the model maps as a function.
    ///     </para>
    /// </remarks>
    ServerContextStub = 17,
}
