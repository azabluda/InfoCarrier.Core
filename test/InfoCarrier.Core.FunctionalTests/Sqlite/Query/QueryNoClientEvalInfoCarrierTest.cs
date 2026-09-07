// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>QueryNoClientEvalTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own
///     <c>QueryNoClientEvalSqliteTest</c>, which is likewise a bare class over the shared Northwind
///     fixture and overrides nothing.
/// </summary>
/// <remarks>
///     <para>
///         The base asserts that an untranslatable operator is <em>refused</em> rather than run on
///         the client, which is this provider's own rule
///         (<c>QuerySplitter.RejectClientEvaluation</c>) stated by someone else's tests. Of its
///         fourteen tests twelve pass, one is skipped by EF itself (<c>Throws_when_group_by</c>,
///         EF issue #18923) and one is red.
///     </para>
///     <para>
///         <b>Two of the three reds this paragraph used to list are now green (R96).</b>
///         <c>Doesnt_throw_when_from_sql_not_composed</c> and <c>Throws_when_from_sql_composed</c>
///         were called "permanently red until #60 is decided"; #60 was decided, the fixture opts
///         into the raw-SQL grant, and both pass. <b>Neither half would have done it alone</b> —
///         the first died on <c>NorthwindQueryRelationalFixture</c>'s
///         <c>(RelationalTestStore)TestStore</c> cast before reaching the query at all, which is
///         what R96 revived R77 for.
///     </para>
///     <para>
///         <b>The one red is not a defect of this provider.</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>Throws_when_orderby_multiple</c> — <b>a message-text difference on this
///                 test, and the label hides what its GREEN siblings do</b> (R67, measured again
///                 in V14). The query has two untranslatable operators:
///                 <c>OrderBy(c =&gt; c.IsLondon).ThenBy(c =&gt; ClientMethod(c))</c>. EF translates
///                 bottom-up and names the inner one
///                 (<c>Translation of member 'IsLondon' … failed</c>); this provider refuses at the
///                 client boundary and names the outer one
///                 (<c>Translation of method '…ClientMethod' failed</c>). <b>Both messages carry
///                 the details clause the base asserts on</b>, and both reasons are true. Naming
///                 <c>IsLondon</c> instead is not reachable from here: the member is not client
///                 code by this provider's test, so only the server could name it, and the server
///                 never sees a query the client has already refused.
///                 <para>
///                     <b>Which means the siblings pass because the query TRAVELS.</b> A probe on
///                     the fault path measured it: <c>Where(c =&gt; c.IsLondon)</c> and
///                     <c>OrderBy(c =&gt; c.IsLondon)</c> both come back with
///                     <c>InfoCarrier.ServerStackTrace</c> set, so the client shipped an access to
///                     an unmapped member and the SERVER refused it. EF Core refuses both in
///                     process, before it opens a connection. This test is the odd one out only
///                     because its second operator is client code, which the client does refuse
///                     locally. <b>So the class is not "message text": it is
///                     <c>ServerBoundaryAnalyzer</c> never asking the client model whether a member
///                     is mapped</b>, which is R138, and the difference is invisible while both
///                     halves build one model from one <c>OnModelCreating</c>.
///                 </para>
///             </description>
///         </item>
///     </list>
/// </remarks>
public class QueryNoClientEvalInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : QueryNoClientEvalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
