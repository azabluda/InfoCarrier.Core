// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     Present in a <em>server's</em> service collection when that server permits a wire payload
///     to carry SQL it will execute (#60).
/// </summary>
/// <remarks>
///     <para>
///         <b>The presence of this service is the whole grant.</b> It carries no data: there is
///         nothing to enumerate, because SQL text is not a naming question and no list of
///         permitted statements is checkable. Registered with
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />,
///         and absent by default.
///     </para>
///     <para>
///         <b>What it grants is arbitrary SQL execution, not a query feature</b>, and the name
///         says so on purpose. <c>Sqlite/RawSqlExecutionProbeTest</c> (R94) measures both halves
///         of why: one <c>CommandText</c> executes every statement it contains, so
///         <c>SELECT 1; DROP TABLE X</c> drops the table; and an uncomposed <c>FromSqlRaw</c>
///         reaches the store unwrapped, so the <c>FROM (&#8230;)</c> subquery that would have
///         confined a payload to reading is not a property of the feature - the caller decides
///         whether to compose. There is therefore no read-only version of this to offer.
///     </para>
///     <para>
///         <b>The server half is the security boundary; the client half
///         (<see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />) is
///         not.</b> Same division as
///         <see cref="Expressions.IInfoCarrierAllowedTypes" />: the client's option governs code
///         the application already controls, and widening it can only produce a query this server
///         refuses. Read <c>docs/security-review.md</c> section 5a before registering, in
///         particular what a deployment gives up - a raw-SQL query does not go through the
///         server's model, so the server's own query filters are not in the statement.
///     </para>
/// </remarks>
public interface IInfoCarrierArbitrarySqlExecution
{
}
