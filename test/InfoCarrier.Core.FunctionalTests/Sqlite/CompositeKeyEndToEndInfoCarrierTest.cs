// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>CompositeKeyEndToEndTestBase</c> on ADR-009 <b>Tier B</b>: save and re-read entities keyed by more
///     than one property.
/// </summary>
/// <remarks>
///     <para>
///         Every key path in this provider is written for a composite key — the identity map lookups
///         in <c>ClientResultMaterializer</c>, the correlation of store-generated values, the
///         shared-identity pairing in <c>ServerSaveChangesExecutor</c> — but almost everything
///         exercising them so far has had a single-property key.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class CompositeKeyEndToEndInfoCarrierTest(CompositeKeyEndToEndInfoCarrierTest.InfoCarrierFixture fixture)
    : CompositeKeyEndToEndTestBase<CompositeKeyEndToEndInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>EF's own SQLite class skips it: SQLite generates no value for a key of more than one column.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/CompositeKeyEndToEndSqliteTest.cs", 12, 14,
        Justification = "Not supported on Sqlite",
        Skip = true)]
    public override Task Can_use_generated_values_in_composite_key_end_to_end()
        => Task.CompletedTask;

    public class InfoCarrierFixture : CompositeKeyEndToEndFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "CompositeKeyEndToEndInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
