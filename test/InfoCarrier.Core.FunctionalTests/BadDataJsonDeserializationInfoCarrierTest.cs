// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>BadDataJsonDeserializationTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     Malformed JSON handed to every mapped type's <c>JsonValueReaderWriter</c>, asserting that
///     each one refuses it rather than returning nonsense. The counterpart to A63's
///     <c>JsonTypes</c>: that base proves the JSON form round-trips, this one proves it fails
///     loudly, and both matter here because A34 made that reader/writer this provider's fallback
///     for any value the wire has no primitive for.
///     <para>
///         No store is involved — the base builds a model and reads JSON directly — so the client
///         only needs a provider to be configured at all.
///         <see cref="InfoCarrierTestHelpers.UseProviderOptions" /> supplies one whose client
///         throws if anything ever reaches it, which is the honest wiring for a test that must not.
///     </para>
/// </remarks>
public class BadDataJsonDeserializationInfoCarrierTest : BadDataJsonDeserializationTestBase
{
    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => base.OnConfiguring(InfoCarrierTestHelpers.Instance.UseProviderOptions(optionsBuilder));
}
