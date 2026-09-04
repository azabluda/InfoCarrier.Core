// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     ADR-009 Tier B: the spec suite over a client whose backing store is a relational database.
/// </summary>
/// <remarks>
///     <para>
///         <b>Every override here is a per-store answer to a store-neutral question.</b> The
///         reason used to be a compile line: the harness was its own project, referenced by Tier A
///         as well, and a reference is transitive, so a relational type named there would have put
///         the relational package on Tier A. That reason is gone with the package and with the
///         project (D3 superseded; R135, R136, R137). The shape stays because it is the better
///         one: <see cref="InfoCarrierTier" /> asks the question and each store answers it.
///     </para>
///     <para>
///         <b>This is the whole difference between the two tiers</b>, in one file: which backend
///         store, which client shell, which client services, which logger factory. Before the
///         split it was a <c>relationalClientStore</c> flag and two branches inside the shared
///         factory, which asked politely and could be got wrong per fixture.
///     </para>
/// </remarks>
public sealed class SqliteInfoCarrierTier : InfoCarrierTier
{
    /// <summary>
    ///     The one instance. A tier is stateless, and every fixture in this project names this.
    /// </summary>
    public static SqliteInfoCarrierTier Instance { get; } = new();

    /// <inheritdoc />
    public override InfoCarrierBackendTestStore CreateBackend(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        => new SqliteInfoCarrierBackendTestStore(name, shared, testStoreProperties);

    /// <inheritdoc />
    /// <remarks>
    ///     Only where the fixture asked. See <see cref="RelationalInfoCarrierTestStore" /> for why
    ///     the choice is per fixture rather than per tier: a relational client shell answers
    ///     <c>ConnectionString</c> and refuses <c>Connection</c>, and only the bases that read the
    ///     first want it.
    /// </remarks>
    public override TestStore CreateClientStore(
        InfoCarrierBackendTestStore backend,
        bool relationalClientStore)
        => relationalClientStore
            ? new RelationalInfoCarrierTestStore(backend)
            : base.CreateClientStore(backend, relationalClientStore);

    /// <inheritdoc />
    /// <remarks>
    ///     A <see cref="TestSqlLoggerFactory" /> rather than a bare <c>ListLoggerFactory</c>, which
    ///     it derives from. Several relational spec fixtures expose <c>TestSqlLoggerFactory</c> as
    ///     a non-virtual property that simply casts this one, and their bases read it:
    ///     <c>ComplexCollectionJsonUpdateTestBase.SuspendRecordingEvents()</c> does, and failed all
    ///     18 of its tests on that cast before the harness returned one. On a client with no
    ///     database it records no SQL and costs nothing.
    /// </remarks>
    public override ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);
}
