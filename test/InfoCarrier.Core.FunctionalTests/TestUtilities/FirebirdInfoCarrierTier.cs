// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     ADR-009 Tier C: the spec suite over a Firebird-backed server.
/// </summary>
/// <remarks>
///     <para>
///         <b>Identical to Tier B in every answer but the store, and that is the point.</b>
///         Firebird is relational, so it wants the same relational client shell and the same
///         <see cref="TestSqlLoggerFactory" /> Tier B wants.
///     </para>
///     <para>
///         <b>This tier is expected to host very few bases.</b> A base belongs to exactly one
///         tier, and only a base that NEEDS a table-valued function or <c>APPLY</c> belongs here;
///         everything else stays where its green already means something. Running a base on two
///         tiers is duplication, not coverage.
///     </para>
/// </remarks>
public sealed class FirebirdInfoCarrierTier : InfoCarrierTier
{
    /// <summary>
    ///     The one instance. A tier is stateless, and every fixture in this project names this.
    /// </summary>
    public static FirebirdInfoCarrierTier Instance { get; } = new();

    /// <inheritdoc />
    public override InfoCarrierBackendTestStore CreateBackend(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        => new FirebirdInfoCarrierBackendTestStore(name, shared, testStoreProperties);

    /// <inheritdoc />
    public override TestStore CreateClientStore(
        InfoCarrierBackendTestStore backend,
        bool relationalClientStore)
        => relationalClientStore
            ? new RelationalInfoCarrierTestStore(backend)
            : base.CreateClientStore(backend, relationalClientStore);

    /// <inheritdoc />
    public override ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new TestSqlLoggerFactory(shouldLogCategory);
}
