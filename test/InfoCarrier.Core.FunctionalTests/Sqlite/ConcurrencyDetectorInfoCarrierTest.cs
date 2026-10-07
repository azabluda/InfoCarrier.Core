// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ConcurrencyDetectorEnabledRelationalTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         Two uses of one context at once must be refused, and this provider has to enforce that
///         itself: the round trip is guarded, and so is each row the residual produces (Z1). Both
///         are ours rather than EF's, which is exactly why the base is worth inheriting.
///     </para>
///     <para>
///         <b>A tier MOVE, not an addition</b> — a base belongs to exactly one tier. The class was
///         on Tier A against the core base; R73 re-parents it onto the relational one, which adds
///         a single <c>FromSql</c> theory and nothing else. 16 tests become 18.
///     </para>
///     <para>
///         The fixture grants raw SQL on both halves, so the inherited <c>FromSql</c> theory
///         reaches the concurrency detector and asserts EF's own refusal. Until 2026-10-07 it
///         asserted the missing grant instead. Enabling the grant removed that diagnostic
///         difference without changing the provider.
///     </para>
///     <para>
///         The relational base reaches its store through
///         <c>(Fixture.TestStore as RelationalTestStore)?.NormalizeDelimitersInRawString(sql) ?? sql</c>
///         — an <c>as</c> with a fallback rather than a cast, so ADR-013's blocking shape does not
///         arise here. It is the one base in the <c>FromSql</c> family written that way.
///     </para>
/// </remarks>
public class ConcurrencyDetectorEnabledInfoCarrierTest(
    ConcurrencyDetectorEnabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture fixture)
    : ConcurrencyDetectorEnabledRelationalTestBase<
        ConcurrencyDetectorEnabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture>(fixture)
{
    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions,
                arbitrarySqlExecution: true);
    }
}

/// <summary>
///     <c>ConcurrencyDetectorDisabledRelationalTestBase</c> on Tier B — the same model with the
///     checks off.
/// </summary>
/// <remarks>
///     The fixture keeps the <c>EnableThreadSafetyChecks(false)</c> that replaces rather than
///     extends the base options, which is EF's own arrangement in every provider's version of this
///     class. The fixture grants raw SQL on both halves, so the inherited <c>FromSql</c> theory
///     runs the query with the detector disabled. Until 2026-10-07 its override asserted the
///     missing grant; the configured fixture now runs EF's unchanged test.
/// </remarks>
public class ConcurrencyDetectorDisabledInfoCarrierTest(
    ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture fixture)
    : ConcurrencyDetectorDisabledRelationalTestBase<
        ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture>(fixture)
{
    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions,
                arbitrarySqlExecution: true);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder.EnableThreadSafetyChecks(enableChecks: false);
    }
}
