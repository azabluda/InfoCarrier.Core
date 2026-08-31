// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.InheritanceModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>InheritanceQueryTestBase</c> on Tier A — a TPH hierarchy queried through its base set.
/// </summary>
/// <remarks>
///     Inheritance is where the wire's type identity has to be exact: a row's entity type is named
///     by the server and resolved by the client, and a discriminator is a shadow property. Nothing
///     adopted queries a hierarchy through its base type.
/// </remarks>
public class InheritanceQueryInfoCarrierTest(InheritanceQueryInfoCarrierFixture fixture)
    : InheritanceQueryTestBase<InheritanceQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     EF's InMemory backend does not enforce foreign keys, and the backend is ours.
    /// </summary>
    protected override bool EnforcesFkConstraints
        => false;

    /// <summary>
    ///     The backing store cannot translate its own defining query, so neither can this.
    /// </summary>
    /// <remarks>
    ///     EF's own <c>InheritanceQueryInMemoryTest</c> overrides this test in exactly this shape,
    ///     for exactly this reason: the keyless view's defining query calls a CLR method
    ///     (<c>MaterializeView</c>) that no provider can translate. The query reaches the server
    ///     whole and the server's InMemory provider refuses it, which is convergence with the
    ///     reference provider rather than a gap of ours.
    /// </remarks>
    public override async Task Can_query_all_animal_views(bool async)
    {
        string message = (await Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Can_query_all_animal_views(async))).Message;

        Assert.Equal(
            CoreStrings.TranslationFailed(
                """
                DbSet<Bird>()
                    .Select(b => InheritanceInfoCarrierServerContext.MaterializeView(b))
                    .OrderBy(a => a.CountryId)
                """),
            message,
            ignoreLineEndingDifferences: true);
    }
}

/// <summary>
///     <c>FiltersInheritanceQueryTestBase</c> — the same hierarchy with query filters on.
/// </summary>
public class FiltersInheritanceQueryInfoCarrierTest(FiltersInheritanceQueryInfoCarrierFixture fixture)
    : FiltersInheritanceQueryTestBase<FiltersInheritanceQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The inheritance query fixture, wired to an InMemory backend behind the wire.
/// </summary>
public class InheritanceQueryInfoCarrierFixture : InheritanceQueryFixtureBase, ITestSqlLoggerFactory
{
    /// <summary>
    ///     The compliance gate's second assertion (R54). The property is real —
    ///     <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> returns a
    ///     <c>TestSqlLoggerFactory</c> — but what it observes is the <em>client's</em> log, and
    ///     this client has no database and emits no SQL. <c>ServerSqlLog</c> is where the
    ///     server's statements can actually be read.
    /// </summary>
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            // The keyless `AnimalQuery` is produced by an InMemory defining query, which is how
            // the *store* makes its rows and therefore no part of the client's model — the same
            // split the Northwind fixture makes for its keyless types.
            serverContextType: typeof(InheritanceInfoCarrierServerContext),
            configureConventions: ConfigureConventions);

    /// <summary>
    ///     Complex types are off, as EF's own InMemory fixture has them: nothing carries a complex
    ///     property over the wire yet.
    /// </summary>
    public override bool EnableComplexTypes
        => false;
}

/// <summary>
///     The filtered variant, differing only in that query filters are on.
/// </summary>
public class FiltersInheritanceQueryInfoCarrierFixture : InheritanceQueryInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;
}

/// <summary>
///     The <em>server-side</em> inheritance context: the shared model plus the InMemory defining
///     query for the keyless <c>AnimalQuery</c>.
/// </summary>
public class InheritanceInfoCarrierServerContext(DbContextOptions options) : InheritanceContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AnimalQuery>().ToInMemoryQuery(() => Set<Bird>().Select(b => MaterializeView(b)));
    }

    // EF's `InheritanceQueryInMemoryFixture.MaterializeView`, restated: it lives in EF's own
    // InMemory functional-test project, which ships in no package this repo references.
    private static AnimalQuery MaterializeView(Bird bird)
        => bird switch
        {
            Kiwi kiwi => new KiwiQuery
            {
                Name = kiwi.Name,
                CountryId = kiwi.CountryId,
                EagleId = kiwi.EagleId,
                FoundOn = kiwi.FoundOn,
                IsFlightless = kiwi.IsFlightless,
            },
            Eagle eagle => new EagleQuery
            {
                Name = eagle.Name,
                CountryId = eagle.CountryId,
                EagleId = eagle.EagleId,
                Group = eagle.Group,
                IsFlightless = eagle.IsFlightless,
            },
            _ => throw new InvalidOperationException(),
        };
}
