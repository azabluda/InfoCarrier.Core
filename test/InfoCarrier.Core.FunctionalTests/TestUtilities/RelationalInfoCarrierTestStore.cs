// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The client test-store shell for the few bases that cast it to <c>RelationalTestStore</c>,
///     opted into per fixture. Identical to <see cref="InfoCarrierTestStore" /> in what it does;
///     the only difference is what it <em>is</em>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a second class rather than one.</b> <c>RelationalTestStore</c> derives from
///         <c>TestStore</c>, so C# cannot give one type both shapes. Making every client store
///         relational would work — it was measured, and it broke nothing — but it would answer
///         ADR-013's own gate question ("does the base assume the <em>client</em> is relational?")
///         with a yes that is not true, for every fixture in the suite. Opting in per fixture keeps
///         that answer honest, and the opt-in list becomes the record of which bases needed it.
///         <b>Per fixture is also the finest grain the harness has</b>: xUnit builds one fixture
///         per test class and the fixture builds the store, so there is no per-test choice to make.
///     </para>
///     <para>
///         <b>What the base offers, and what is refused.</b> Of the members
///         <c>RelationalTestStore</c> adds, the string half is harmless and is the half these bases
///         actually want: <c>ConnectionString</c> (the server's, which is the truthful answer) and
///         <c>NormalizeDelimitersInRawString</c>, whose delimiters already default to SQLite's.
///         The other half — <c>ConnectionState</c>, <c>CloseConnection</c> and
///         <c>BeginTransaction</c> — would reach the database directly, past the wire, and a green
///         from that says nothing about this provider. All three are non-virtual, and all three
///         read <see cref="Connection" />, which is <c>protected virtual</c>: overriding that one
///         member governs every one of them.
///     </para>
///     <para>
///         <b>Measured before it was believed.</b> Making the whole suite relational moved 25
///         <c>InvalidCastException</c> failures to zero, changed no test's status (FIXED none,
///         BROKEN none), and <see cref="Connection" /> was never reached even once — so nothing in
///         this suite wants a live connection and refusing one costs nothing. It also uncovered one
///         real defect the cast had been hiding.
///     </para>
///     <para>
///         <b>One base this must not be pointed at.</b>
///         <c>AdHocQuerySplittingQueryTestBase</c> calls <c>CloseConnection()</c> on the cast
///         store, so satisfying it needs a live connection to the server's database. What it tests
///         — behaviour when a connection drops mid split-query — has no meaning across this wire,
///         so a green there would be manufactured.
///     </para>
/// </remarks>
public class RelationalInfoCarrierTestStore(InfoCarrierBackendTestStore backend)
    : RelationalTestStore(backend.Name, shared: false, backend.CreateStoreConnection()),
      IInfoCarrierClientTestStore
{
    private readonly InfoCarrierBackendTestStore _backend = backend;

    /// <inheritdoc />
    public InfoCarrierBackendTestStore Backend => _backend;

    /// <summary>
    ///     Refused: the InfoCarrier client has no database of its own.
    /// </summary>
    /// <remarks>
    ///     Every relational member that would reach past the wire reads this one, so throwing here
    ///     is the whole guard. See the class remarks.
    /// </remarks>
    protected override DbConnection Connection
        => throw new InvalidOperationException(
            "The InfoCarrier client has no database of its own, so this test store exposes no "
            + "DbConnection. A test reaching for one would bypass the wire, and a green from that "
            + "would say nothing about this provider.");

    /// <inheritdoc />
    /// <remarks>
    ///     Deliberately not <c>base.InitializeAsync</c>: the relational one opens
    ///     <see cref="Connection" /> once initialization returns.
    /// </remarks>
    public override async Task<TestStore> InitializeAsync(
        IServiceProvider? serviceProvider,
        Func<DbContext>? createContext,
        Func<DbContext, Task>? seed = null,
        Func<DbContext, Task>? clean = null)
    {
        await _backend.InitializeAsync(_backend.ServiceProvider, _backend.CreateDbContext, seed, clean)
            .ConfigureAwait(false);
        return this;
    }

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseInfoCarrier(_backend, InfoCarrierTestStore.ClientOptions(_backend));

    /// <inheritdoc />
    public override async Task CleanAsync(DbContext context)
        => await _backend.CleanAsync(_backend.CreateDbContext()).ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    ///     Deliberately not <c>base.DisposeAsync()</c>: the relational one disposes
    ///     <see cref="Connection" />. <c>TestStore.DisposeAsync</c>, the only thing below it, does
    ///     nothing, so nothing is lost.
    /// </remarks>
    public override async ValueTask DisposeAsync()
        => await _backend.DisposeAsync().ConfigureAwait(false);
}
