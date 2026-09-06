// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     ADR-009 Tier A: the spec suite over EF's InMemory provider.
/// </summary>
/// <remarks>
///     <b>Every default on <see cref="InfoCarrierTier" /> is the right answer here</b>, and this
///     class overrides only the one thing a tier must say for itself. That is not an accident: the
///     defaults ARE the store-neutral answers, and InMemory is the store that wants nothing else.
///     A tier needing more is a tier whose store is doing something the wire has to know about.
/// </remarks>
public sealed class InMemoryInfoCarrierTier : InfoCarrierTier
{
    /// <inheritdoc />
    public override InfoCarrierBackendTestStore CreateBackend(
        string name,
        bool shared,
        SharedTestStoreProperties testStoreProperties)
        => new InMemoryInfoCarrierBackendTestStore(name, shared, testStoreProperties);
}
