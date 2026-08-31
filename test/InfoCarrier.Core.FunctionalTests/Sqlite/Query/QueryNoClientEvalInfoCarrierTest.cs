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
///         fourteen tests ten pass, one is skipped by EF itself (<c>Throws_when_group_by</c>,
///         EF issue #18923) and three are red.
///     </para>
///     <para>
///         <b>None of the three reds is a defect of this provider.</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>Doesnt_throw_when_from_sql_not_composed</c> and
///                 <c>Throws_when_from_sql_composed</c> — issue #60. Both need a relational
///                 <c>FromSql</c> on the client; the first does not even reach it, dying on
///                 <c>NorthwindQueryRelationalFixture</c>'s <c>(RelationalTestStore)TestStore</c>
///                 cast first. Permanently red until #60 is decided.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>Throws_when_orderby_multiple</c> — <b>a message-text difference, not a
///                 gap</b> (step R67). The query has two untranslatable operators:
///                 <c>OrderBy(c =&gt; c.IsLondon).ThenBy(c =&gt; ClientMethod(c))</c>. EF translates
///                 bottom-up and names the inner one
///                 (<c>Translation of member 'IsLondon' … failed</c>); this provider refuses at the
///                 client boundary and names the outer one
///                 (<c>Translation of method '…ClientMethod' failed</c>). <b>Both messages carry
///                 the details clause the base asserts on</b>, and both reasons are true. Naming
///                 <c>IsLondon</c> instead is not reachable from here: the member is not client
///                 code by this provider's test, so only the server could name it, and the server
///                 never sees a query the client has already refused.
///             </description>
///         </item>
///     </list>
/// </remarks>
public class QueryNoClientEvalInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : QueryNoClientEvalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
