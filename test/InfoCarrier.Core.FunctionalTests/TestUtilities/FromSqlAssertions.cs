// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Asserts that a <c>FromSql</c> query is <em>refused</em> rather than answered.
/// </summary>
/// <remarks>
///     <para>
///         <b>This provider does not support <c>FromSql</c>, and until R75 it did not say so.</b>
///         The wire's query-root node carries an entity type and nothing else, and
///         <c>FromSqlQueryRootExpression</c> is a <em>subclass</em> of the root the node was
///         written for — so the SQL text and its parameters fell into the base-class branch and
///         were dropped. A <c>FromSqlRaw</c> carrying a <c>WHERE</c> came back as the whole table,
///         with no diagnostic. R75 refuses the subclass instead.
///     </para>
///     <para>
///         <b>Asserting the refusal, rather than skipping the test.</b> An unimplemented feature is
///         left red where the base is the only statement of it, but where the base's own premise
///         is that the query <em>runs</em>, the useful thing to pin is the contract this provider
///         actually offers — which is what <c>QuerySplitter</c>'s own remarks call the clearest
///         statement of it. These assertions are also the tripwire: <b>if <c>FromSql</c> is ever
///         supported, every one of them fails</b> and the decision has to be taken again
///         deliberately.
///     </para>
/// </remarks>
public static class FromSqlAssertions
{
    /// <summary>
    ///     The refusal names the offending node, so the message is asserted on that rather than on
    ///     wording that may be reworded.
    /// </summary>
    private const string RefusedNode = "FromSqlQueryRootExpression";

    /// <summary>Asserts that <paramref name="query" /> is refused.</summary>
    public static void NotSupported(Action query)
        => Assert.Contains(RefusedNode, Assert.Throws<InvalidOperationException>(query).Message);

    /// <summary>Asserts that <paramref name="query" /> is refused.</summary>
    public static async Task NotSupportedAsync(Func<Task> query)
        => Assert.Contains(
            RefusedNode,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}
