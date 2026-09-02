// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     One backing store the spec suite runs against, and everything the harness has to do
///     differently for it (ADR-009).
/// </summary>
/// <remarks>
///     <para>
///         <b>This replaced a delegate and a bool, and the reason is the project split.</b> The
///         harness used to pick the backend store through a
///         <c>Func&lt;…, InfoCarrierBackendTestStore&gt;</c> and decide the rest from a
///         <c>relationalClientStore</c> flag, which meant the shared harness had to NAME the
///         relational client store, the relational logger factory and the relational client
///         services. Naming them means referencing them, a reference is transitive, and Tier A
///         would have had the relational package on its own compile line. A relational client over
///         an InMemory backend is exactly the disagreement the seam exists to prevent
///         (<c>architecture.md</c> §6a <b>D3</b>).
///     </para>
///     <para>
///         So each tier answers for itself, from its own assembly. Everything relational is
///         overridden in <c>InfoCarrier.Core.Relational.FunctionalTests</c>; the defaults here are
///         the store-neutral answers, and a tier that says nothing gets them.
///     </para>
/// </remarks>
public abstract class InfoCarrierTier
{
    /// <summary>
    ///     Creates the server-side backend store this tier runs against.
    /// </summary>
    public abstract InfoCarrierBackendTestStore CreateBackend(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties);

    /// <summary>
    ///     The client shell handed to a fixture.
    /// </summary>
    /// <remarks>
    ///     <paramref name="relationalClientStore" /> is honoured only by a tier whose store is
    ///     relational; there is nothing for a non-relational one to return.
    /// </remarks>
    public virtual TestStore CreateClientStore(
        InfoCarrierBackendTestStore backend,
        bool relationalClientStore)
        => new InfoCarrierTestStore(backend);

    /// <summary>
    ///     The tier's own additions to the <em>client's</em> provider services.
    /// </summary>
    /// <remarks>
    ///     Nothing by default. The relational tier registers
    ///     <c>AddInfoCarrierRelationalClient()</c> here, gated on the same raw-SQL grant as its
    ///     server half, because <c>Database.SqlQuery&lt;T&gt;</c> needs both.
    /// </remarks>
    public virtual IServiceCollection AddClientServices(
        IServiceCollection services,
        bool arbitrarySqlExecution)
        => services;

    /// <summary>
    ///     The logger factory a fixture observes.
    /// </summary>
    /// <remarks>
    ///     A plain <see cref="ListLoggerFactory" /> by default. The relational tier returns a
    ///     <c>TestSqlLoggerFactory</c>, which several relational spec fixtures cast to without
    ///     asking — <c>ComplexCollectionJsonUpdateTestBase.SuspendRecordingEvents()</c> does, and
    ///     failed all 18 of its tests on that cast before the harness returned one.
    /// </remarks>
    public virtual ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new(shouldLogCategory);
}
