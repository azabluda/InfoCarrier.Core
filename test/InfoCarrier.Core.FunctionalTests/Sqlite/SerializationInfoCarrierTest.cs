// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     The F1 model on ADR-009 <b>Tier B</b>, shared by <see cref="SerializationInfoCarrierTest" />
///     and <see cref="DataBindingInfoCarrierTest" /> as EF shares its own <c>F1SqliteFixture</c>
///     with <c>OptimisticConcurrencySqliteTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture" /> with a store of its
///         own. That class recreates its store before every test, and the test classes of this
///         suite run in parallel, so on one store these two would read a database being rebuilt.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare these bases with plain EF; Tier A's store runs no statement. On Tier A the
///         fixture was a class of its own, building the server's model over InMemory's convention
///         set, and it registered the whole of <c>F1MaterializationInterceptor</c> on the server.
///         This one registers only the construction half, for the reason its base class gives.
///     </para>
/// </remarks>
public class F1InfoCarrierFixture : OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName
        => "F1InfoCarrierTest";
}

/// <summary>
///     <c>SerializationTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     A tracked graph serialized with <c>System.Text.Json</c> and Newtonsoft, which is a pointed
///     thing to ask of this provider: the entities under test were built by
///     <c>ClientResultMaterializer</c> rather than by EF's shaper, and a navigation left pointing
///     at a half-built instance shows up here as a cycle rather than as a wrong answer.
/// </remarks>
public class SerializationInfoCarrierTest(F1InfoCarrierFixture fixture)
    : SerializationTestBase<F1InfoCarrierFixture>(fixture);

/// <summary>
///     <c>DataBindingTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <c>ToObservableCollection</c> / <c>Local</c> over the change tracker: the binding lists are
///     built from tracked entries, so this is a check that what this provider puts in the tracker
///     behaves like what EF's shaper puts there.
/// </remarks>
public class DataBindingInfoCarrierTest(F1InfoCarrierFixture fixture)
    : DataBindingTestBase<F1InfoCarrierFixture>(fixture);
