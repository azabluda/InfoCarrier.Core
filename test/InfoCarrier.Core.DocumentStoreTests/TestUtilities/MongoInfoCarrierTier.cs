// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;

namespace InfoCarrier.Core.DocumentStoreTests.TestUtilities;

/// <summary>
///     ADR-009 Tier D: the specification bases over an embedded MongoDB.
/// </summary>
/// <remarks>
///     <para>
///         The fourth tier, and the first whose store is not relational. Like
///         <c>InMemoryInfoCarrierTier</c> it overrides only <see cref="CreateBackend" />: the
///         defaults on <see cref="InfoCarrierTier" /> are the store-neutral answers, and what this
///         store needs differently it says on
///         <see cref="MongoInfoCarrierBackendTestStore" /> instead.
///     </para>
///     <para>
///         <b>The client half of #100 is not set here and does not need to be.</b>
///         <c>InfoCarrierTestStore.ClientOptions</c> reads
///         <c>InfoCarrierBackendTestStore.ServerStoreIsRelational</c> and calls
///         <c>UseNonRelationalServerStore()</c> for a store that says it is not relational, so the
///         two halves cannot disagree about it.
///     </para>
/// </remarks>
public sealed class MongoInfoCarrierTier : InfoCarrierTier
{
    /// <summary>
    ///     The one instance. A tier is stateless, and every Tier D specification fixture names this.
    /// </summary>
    public static MongoInfoCarrierTier Instance { get; } = new();

    /// <inheritdoc />
    public override InfoCarrierBackendTestStore CreateBackend(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        => new MongoInfoCarrierBackendTestStore(name, shared, testStoreProperties);
}
