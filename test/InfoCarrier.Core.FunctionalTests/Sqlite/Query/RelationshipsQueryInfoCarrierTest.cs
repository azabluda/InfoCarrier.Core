// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     Inheritance relationships under TPT and TPC, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>The step up from the inheritance bases already adopted.</b> Those query a hierarchy;
///         these navigate <em>between</em> hierarchies, so a single query touches the store objects
///         of a derived type, its base, and the derived types on the far side of a navigation.
///         Under TPT that is several joins the client never sees, which makes it the sharpest test
///         so far that this client hands over a model the server resolves identically.
///     </para>
///     <para>
///         <b>EF disables its own TPC class and gives no reason.</b>
///         <c>TPCRelationshipsQuerySqliteTest</c> is declared <c>internal</c>, which stops xUnit
///         collecting it, with no comment saying why. That is a signal and not a verdict, so this
///         one is adopted as an ordinary public class: a red test here is information (ADR-004),
///         and a silently disabled one is what v1 did.
///     </para>
/// </remarks>
public class TPTRelationshipsQueryInfoCarrierTest(TPTRelationshipsQueryInfoCarrierFixture fixture)
    : TPTRelationshipsQueryTestBase<TPTRelationshipsQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="TPTRelationshipsQueryInfoCarrierTest" />
public class TPCRelationshipsQueryInfoCarrierTest(TPCRelationshipsQueryInfoCarrierFixture fixture)
    : TPCRelationshipsQueryTestBase<TPCRelationshipsQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The TPT relationships fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPTRelationshipsQueryInfoCarrierFixture : TPTRelationshipsQueryRelationalFixture
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

/// <summary>
///     The TPC relationships fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPCRelationshipsQueryInfoCarrierFixture : TPCRelationshipsQueryRelationalFixture
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
