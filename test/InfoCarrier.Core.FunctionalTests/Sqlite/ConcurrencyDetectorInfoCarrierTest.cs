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
///         <b>Sixteen of the eighteen assert the detector; the <c>FromSql</c> theory asserts the
///         refusal instead (R75).</b> R62 predicted "2 tests, both red, zero new green" and R73
///         measured both green — but they were green because <c>FromSql</c> was silently
///         discarded. Now the query is refused while it is still being compiled, which is
///         <em>earlier</em> than the detector's check, so the concurrency message the base expects
///         is never the one that arrives. Both statements are true and only one is observable.
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
    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>The refusal arrives before the detector does.</b> The base enters a critical
    ///         section on another thread and asserts that the call raises
    ///         <c>CoreStrings.ConcurrentMethodInvocation</c>; on this provider the query is refused
    ///         (R75) while it is still being compiled, which is <em>earlier</em> than the
    ///         detector's check. Both statements are true and only one is observable. The other
    ///         sixteen tests in this class still assert the detector.
    ///     </para>
    ///     <para>
    ///         <b>Wrapping <c>base</c> in an assertion cannot work here, and this is A63's shape
    ///         for the third time</b> (R70 recorded it for <c>JsonQuery</c>'s four APPLY tests).
    ///         <c>ConcurrencyDetectorEnabledTestBase.ConcurrencyDetectorTest</c> catches the
    ///         <see cref="InvalidOperationException" /> <em>itself</em> and compares its message,
    ///         so what escapes <c>base</c> is an <c>Xunit.Sdk.EqualException</c> and any
    ///         <c>Assert.Throws&lt;InvalidOperationException&gt;</c> around it fails with
    ///         "Exception type was not an exact match". EF's own <c>Task.CompletedTask</c> form is
    ///         taken instead, rather than re-writing the query outside the base and pinning this
    ///         file to EF's SQL text.
    ///     </para>
    ///     <para>
    ///         <b>The refusal itself is not left unasserted</b> — the disabled sibling below pins
    ///         it on the same query, where the base adds no assertion of its own to collide with.
    ///     </para>
    /// </remarks>
    public override Task FromSql(bool async)
        => Task.CompletedTask;

    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}

/// <summary>
///     <c>ConcurrencyDetectorDisabledRelationalTestBase</c> on Tier B — the same model with the
///     checks off.
/// </summary>
/// <remarks>
///     The fixture keeps the <c>EnableThreadSafetyChecks(false)</c> that replaces rather than
///     extends the base options, which is EF's own arrangement in every provider's version of this
///     class. With the checks off the base simply runs the query and asserts nothing, which is why
///     its <c>FromSql</c> theory <b>used to pass by accident</b>: the query root was discarded and
///     the resulting table scan raised nothing. R75 refuses it, and the refusal is pinned instead.
/// </remarks>
public class ConcurrencyDetectorDisabledInfoCarrierTest(
    ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture fixture)
    : ConcurrencyDetectorDisabledRelationalTestBase<
        ConcurrencyDetectorDisabledInfoCarrierTest.ConcurrencyDetectorInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     With the checks off the base simply runs the query and asserts nothing, so this test
    ///     <b>used to pass by accident</b>: the <c>FromSqlRaw</c> was silently discarded and the
    ///     resulting table scan raised nothing. R75 refuses it, and the refusal is what is pinned.
    /// </remarks>
    public override Task FromSql(bool async)
        => FromSqlAssertions.NotSupportedAsync(() => base.FromSql(async));

    public class ConcurrencyDetectorInfoCarrierFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder.EnableThreadSafetyChecks(enableChecks: false);
    }
}
