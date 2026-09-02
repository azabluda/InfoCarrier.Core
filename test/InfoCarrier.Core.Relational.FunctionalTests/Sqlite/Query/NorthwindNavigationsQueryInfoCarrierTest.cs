// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindNavigationsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes one override.</b> The Tier A class overrode
///         <c>Where_subquery_on_navigation_client_eval</c> with <c>AssertTranslationFailed</c> —
///         client evaluation outside the final projection, which a remoting provider cannot do
///         without fetching the whole table. The relational base carries that exact override, so
///         it is now inherited rather than restated.
///     </para>
///     <para>
///         Deliberately <b>no further overrides</b>. EF's own <c>NorthwindNavigationsQuerySqliteTest</c>
///         adds none either; whether this provider needs any is what the run answers.
///     </para>
/// </remarks>
public class NorthwindNavigationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindNavigationsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
