// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>KeysWithConvertersTestBase</c> on ADR-009 Tier B.
/// </summary>
/// <remarks>
///     <para>
///         A key behind a value converter is the case every identity path in this provider has to
///         get right twice: the client resolves identity from the key array it decoded, and the
///         server re-keys the same row from the wire. `ValueConvertersEndToEnd` covers converters
///         on ordinary properties; this is the same question where the property is the thing rows
///         are found by.
///     </para>
///     <para>
///         <b>Tier B, and it was Tier A until J1.</b> Nothing about a converted key needs a
///         relational store — but Tier A brought <b>seven</b> skips with it, and every one was
///         EF's own `KeysWithConvertersInMemoryTest` refusing issue #26238: a key whose CLR type is
///         an <c>IEnumerable</c>, and the three entity types that use one, which that fixture has
///         to <c>Ignore</c> outright. <c>KeysWithConvertersSqliteTest</c> skips <b>none</b> of
///         them and ignores nothing. So seven of the eight shapes this base exists to cover were
///         being asserted about the backing store rather than about this provider, which is
///         CLAUDE.md's A79/A80 rule exactly: the tier that translates is the one whose green means
///         more.
///     </para>
/// </remarks>
public class KeysWithConvertersInfoCarrierTest(KeysWithConvertersInfoCarrierTest.InfoCarrierFixture fixture)
    : KeysWithConvertersTestBase<KeysWithConvertersInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : KeysWithConvertersFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: IgnoreCollectionKeyComparerWarning,
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => IgnoreCollectionKeyComparerWarning(base.AddOptions(builder));

        /// <summary>
        ///     <c>EnumerableClassKey*</c>'s key is an <c>IEnumerable</c> behind a value converter
        ///     and EF's test model gives it no value comparer, so model validation warns.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>This is EF's own behaviour, expressed the only way this fixture can express
        ///         it.</b> <c>KeysWithConvertersSqliteFixture.AddOptions</c> is
        ///         <c>builder.UseSqlite(…)</c> and <b>never chains to base</b> — so
        ///         <c>FixtureBase.AddOptions</c>'s <c>ConfigureWarnings(Default(Throw))</c> never
        ///         runs there and the warning stays a warning. This client cannot take that route,
        ///         because <c>UseSqlite</c> is exactly what it does not do; ignoring the one event
        ///         id reaches the same state without also discarding the rest of the base's
        ///         options.
        ///     </para>
        ///     <para>
        ///         Applied to <b>both</b> halves. The model is validated twice, once per side
        ///         (A49), so configuring one would leave the other throwing — the asymmetry that
        ///         CLAUDE.md's "computed twice by two providers" rule exists to catch.
        ///     </para>
        /// </remarks>
        private static DbContextOptionsBuilder IgnoreCollectionKeyComparerWarning(DbContextOptionsBuilder builder)
            => builder.ConfigureWarnings(w => w.Ignore(CoreEventId.CollectionWithoutComparer));
    }
}
