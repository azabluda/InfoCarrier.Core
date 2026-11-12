// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

// EF Core 11 separates complex-property inheritance queries by storage mapping.

public class TPHInheritanceJsonQueryInfoCarrierTest(
    TPHInheritanceJsonQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPHInheritanceJsonQueryRelationalTestBase<TPHInheritanceJsonQueryInfoCarrierFixture>(fixture, output);

public class TPHInheritanceTableSplittingQueryInfoCarrierTest(
    TPHInheritanceQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPHInheritanceTableSplittingQueryRelationalTestBase<TPHInheritanceQueryInfoCarrierFixture>(fixture, output);

public class TPHInheritanceJsonQueryInfoCarrierFixture : TPHInheritanceJsonQueryRelationalFixtureBase
{
    private ITestStoreFactory? _factory;

    protected override string StoreName => "TPHInheritanceJsonInfoCarrier";

    protected override ITestStoreFactory TestStoreFactory
        => _factory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance, ContextType,
            (model, context) => OnModelCreating(model, context),
            configureConventions: ConfigureConventions);
}

public class TPTInheritanceJsonQueryInfoCarrierTest(
    TPTInheritanceJsonQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPTInheritanceJsonQueryRelationalTestBase<TPTInheritanceJsonQueryInfoCarrierFixture>(fixture, output);

public class TPTInheritanceTableSplittingQueryInfoCarrierTest(
    TPTInheritanceQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPTInheritanceTableSplittingQueryRelationalTestBase<TPTInheritanceQueryInfoCarrierFixture>(fixture, output);

public class TPTInheritanceJsonQueryInfoCarrierFixture : TPTInheritanceJsonQueryRelationalFixtureBase
{
    private ITestStoreFactory? _factory;

    protected override string StoreName => "TPTInheritanceJsonInfoCarrier";

    protected override ITestStoreFactory TestStoreFactory
        => _factory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance, ContextType,
            (model, context) => OnModelCreating(model, context),
            configureConventions: ConfigureConventions);
}

public class TPCInheritanceJsonQueryInfoCarrierTest(
    TPCInheritanceJsonQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPCInheritanceJsonQueryRelationalTestBase<TPCInheritanceJsonQueryInfoCarrierFixture>(fixture, output);

public class TPCInheritanceTableSplittingQueryInfoCarrierTest(
    TPCInheritanceQueryInfoCarrierFixture fixture, ITestOutputHelper output)
    : TPCInheritanceTableSplittingQueryRelationalTestBase<TPCInheritanceQueryInfoCarrierFixture>(fixture, output);

public class TPCInheritanceJsonQueryInfoCarrierFixture : TPCInheritanceJsonQueryRelationalFixtureBase
{
    private ITestStoreFactory? _factory;

    protected override string StoreName => "TPCInheritanceJsonInfoCarrier";

    protected override ITestStoreFactory TestStoreFactory
        => _factory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance, ContextType,
            (model, context) => OnModelCreating(model, context),
            configureConventions: ConfigureConventions);
}
