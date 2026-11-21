// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 FromSqlQueryCosmosTest. SQL inputs remain; golden baselines are omitted.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications.CosmosSql;

using Customer = Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer;

public class FromSqlQueryTest : QueryTestBase<CosmosRawSqlFixture>
{
    private static readonly string _eol = Environment.NewLine;
    private static void SkipOnLinuxEmulator()
        => throw Xunit.Sdk.SkipException.ForSkip("Linux emulator issue 335: projection query exclusion.");

    public FromSqlQueryTest(
        CosmosRawSqlFixture fixture)
        : base(fixture)
    {
    }

    protected NorthwindContext CreateContext()
        => Fixture.CreateContext();

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_simple(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["ContactName"] LIKE '%z%'""");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(14, actual.Length);
                Assert.Equal(14, context.ChangeTracker.Entries().Count());


            });

    [Fact]
    public async Task FromSqlRaw_queryable_incorrect_discriminator_throws()
    {
        using var context = CreateContext();
        var query = context.Set<Order>().FromSqlRaw(
            """
SELECT * FROM root c WHERE c["$type"] = "OrderDetail"
""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            CoreStrings.UnableToDiscriminate(context.Model.FindEntityType(typeof(Order))!.DisplayName(), "OrderDetail"),
            exception.Message);
    }

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_simple_columns_out_of_order(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """
SELECT c["id"], c["$type"], c["Region"], c["PostalCode"], c["Phone"], c["Fax"], c["Country"], c["ContactTitle"], c["ContactName"], c["CompanyName"], c["City"], c["Address"] FROM root c WHERE c["$type"] = "Customer"
""");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Equal(91, context.ChangeTracker.Entries().Count());


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_simple_columns_out_of_order_and_extra_columns(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """
SELECT c["id"], c["$type"], c["Region"], c["PostalCode"], c["PostalCode"] AS Foo, c["Phone"], c["Fax"], c["Country"], c["ContactTitle"], c["ContactName"], c["CompanyName"], c["City"], c["Address"] FROM root c WHERE c["$type"] = "Customer"
""");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Equal(91, context.ChangeTracker.Entries().Count());


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_composed(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """
                    SELECT * FROM root c WHERE c["$type"] = "Customer"
                    """)
                    .Where(c => c.ContactName!.Contains("z"));

                var sql = query.ToQueryString();

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(14, actual.Length);
                Assert.Equal(14, context.ChangeTracker.Entries().Count());


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_composed_after_removing_whitespaces(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        _eol + "    " + _eol + _eol + _eol + "SELECT" + _eol + @"* FROM root c WHERE c[""$type""] = ""Customer""")
                    .Where(c => c.ContactName!.Contains("z"));

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(14, actual.Length);


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_composed_compiled(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                if (a)
                {
                    var query = EF.CompileAsyncQuery((NorthwindContext context) => context.Set<Customer>()
                        .FromSqlRaw(
                            """
SELECT * FROM root c WHERE c["$type"] = "Customer"
""")
                        .Where(c => c.ContactName!.Contains("z")));

                    using (var context = CreateContext())
                    {
                        var actual = await query(context).ToListAsync();

                        Assert.Equal(14, actual.Count);
                    }
                }
                else
                {
                    var query = EF.CompileQuery((NorthwindContext context) => context.Set<Customer>()
                        .FromSqlRaw("""SELECT * FROM root c WHERE c["$type"] = "Customer" """)
                        .Where(c => c.ContactName!.Contains("z")));

                    using (var context = CreateContext())
                    {
                        var actual = query(context).ToArray();

                        Assert.Equal(14, actual.Length);
                    }
                }


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    [InfoCarrierDesign(6, Deviation = DeviationKind.SqlDiffers | DeviationKind.Other,
        Justification = "A compiled client query ships a raw tree and values for ordinary backend query translation.",
        DeviationNote = "The raw CONSH argument remains a bound parameter on the server instead of the direct compiled query's literal. Also assert the returned CustomerID, beyond upstream's single-row count.")]
    public virtual Task FromSqlRaw_queryable_composed_compiled_with_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                if (a)
                {
                    var query = EF.CompileAsyncQuery((NorthwindContext context) => context.Set<Customer>().FromSqlRaw(
                            """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["id"] = {0}""", "CONSH")
                        .Where(c => c.ContactName!.Contains("z")));

                    using (var context = CreateContext())
                    {
                        var actual = await query(context).ToListAsync();

                        Assert.Single(actual);
                        Assert.Equal("CONSH", actual[0].CustomerID);
                    }
                }
                else
                {
                    var query = EF.CompileQuery((NorthwindContext context) => context.Set<Customer>().FromSqlRaw(
                            """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["id"] = {0}""", "CONSH")
                        .Where(c => c.ContactName!.Contains("z")));

                    using (var context = CreateContext())
                    {
                        var actual = query(context).ToArray();

                        Assert.Single(actual);
                        Assert.Equal("CONSH", actual[0].CustomerID);
                    }
                }


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_multiple_line_query(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """
SELECT *
FROM root c
WHERE c["$type"] = "Customer" AND c["City"] = 'London'
""");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_composed_multiple_line_query(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """
SELECT *
FROM root c
WHERE c["$type"] = "Customer"
""")
                    .Where(c => c.City == "London");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_with_parameters(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                var city = "London";
                var contactTitle = "Sales Representative";

                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = {0} AND c["ContactTitle"] = {1}""",
                    city,
                    contactTitle);

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_with_parameters_inline(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                    """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = {0} AND c["ContactTitle"] = {1}""",
                    "London",
                    "Sales Representative");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_with_null_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                uint? reportsTo = null;

                using var context = CreateContext();
                var query = context.Set<Employee>().FromSqlRaw(
                    """SELECT * FROM root c WHERE c["$type"] = "Employee" AND c["ReportsTo"] = {0} OR (IS_NULL(c["ReportsTo"]) AND IS_NULL({0}))""",
                    reportsTo);

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Single(actual);


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public Task FromSqlRaw_queryable_with_parameters_and_closure(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                var city = "London";
                var contactTitle = "Sales Representative";

                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = {0}""", city)
                    .Where(c => c.ContactTitle == contactTitle);
                var queryString = query.ToQueryString();

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_simple_cache_key_includes_query_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>()
                    .FromSqlRaw("""SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = 'London'""");

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));

                query = context.Set<Customer>()
                    .FromSqlRaw("""SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = 'Seattle'""");

                actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Single(actual);
                Assert.True(actual.All(c => c.City == "Seattle"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_with_parameters_cache_key_includes_parameters(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                var city = "London";
                var contactTitle = "Sales Representative";
                var sql =
                    """SELECT * FROM root c WHERE c["$type"] = "Customer" AND c["City"] = {0} AND c["ContactTitle"] = {1}""";

                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(sql, city, contactTitle);

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));

                city = "Madrid";
                contactTitle = "Accounting Manager";

                query = context.Set<Customer>().FromSqlRaw(sql, city, contactTitle);

                actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(2, actual.Length);
                Assert.True(actual.All(c => c.City == "Madrid"));
                Assert.True(actual.All(c => c.ContactTitle == "Accounting Manager"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_simple_as_no_tracking_not_composed(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """
SELECT * FROM root c WHERE c["$type"] = "Customer"
""")
                    .AsNoTracking();

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Empty(context.ChangeTracker.Entries());


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_queryable_simple_projection_composed(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Product>().FromSqlRaw(
                        """
SELECT *
FROM root c
WHERE c["$type"] = "Product" AND NOT c["Discontinued"] AND ((c["UnitsInStock"] - c["UnitPrice"]) < c["SupplierID"])
""")
                    .Select(p => p.ProductName);

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(37, actual.Length);


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_composed_with_nullable_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """
SELECT * FROM root c WHERE c["$type"] = "Customer"
""")
                    .Where(c => c.ContactName == c.CompanyName);

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Empty(actual);


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSqlRaw_does_not_parameterize_interpolated_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var propertyName = "OrderID";
                var max = 10250;
                var query = context.Orders.FromSqlRaw(
                    $$"""SELECT * FROM root c WHERE c["$type"] = "Order" AND c["{{propertyName}}"] < {0}""", max);

                var actual = a
                    ? await query.ToListAsync()
                    : query.ToList();

                Assert.Equal(2, actual.Count);
            });

    [Theory, MemberData(nameof(IsAsyncData))]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/FromSqlQueryCosmosTest.cs",
        668, 704, Skip = true, Justification = "Linux emulator issue 335: projection query exclusion.")]
    public virtual Task FromSqlRaw_queryable_simple_projection_not_composed(bool async)
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        return Fixture.NoSyncTest(
            async, async a =>
            {
                using var context = CreateContext();
                var query = context.Set<Customer>().FromSqlRaw(
                        """
SELECT * FROM root c WHERE c["$type"] = "Customer"
""")
                    .Select(c => new { c.CustomerID, c.City })
                    .AsNoTracking();

                var actual = a
                    ? await query.ToArrayAsync()
                    : query.ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Empty(context.ChangeTracker.Entries());


            });
    }

    [Fact]
    public async Task FromSqlRaw_queryable_simple_with_missing_discriminator_throws()
    {
        using var context = CreateContext();
        var query = context.Set<Order>()
            .FromSqlRaw("""SELECT c.OrderID FROM root c""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            CoreStrings.UnableToDiscriminate(context.Model.FindEntityType(typeof(Order))!.DisplayName(), null),
            exception.Message);
    }

    [Fact]
    public async Task FromSqlRaw_queryable_simple_with_missing_key_throws()
    {
        using var context = CreateContext();
        var query = context.Set<Order>()
            .FromSqlRaw("""SELECT "Order" AS "$type" FROM root c""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            CoreStrings.InvalidKeyValue(
                context.Model.FindEntityType(typeof(Order))!.DisplayName(),
                "OrderID"),
            exception.Message);
    }

    [Fact]
    public async Task FromSqlRaw_queryable_simple_with_missing_key_and_non_tracking_throws()
    {
        using var context = CreateContext();
        var query = context.Set<Order>()
            .FromSqlRaw("""SELECT "Order" AS "$type" FROM root c""")
            .AsNoTracking();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            CoreStrings.InvalidKeyValue(
                context.Model.FindEntityType(typeof(Order))!.DisplayName(),
                "OrderID"),
            exception.Message);
    }

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSql_queryable_with_parameters_interpolated(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                var city = "London";
                var contactTitle = "Sales Representative";

                await AssertQuery(
                    a,
                    ss => ((DbSet<Customer>)ss.Set<Customer>()).FromSql(
                        $"""SELECT * FROM root c WHERE c["City"] = {city} AND c["ContactTitle"] = {contactTitle}"""),
                    ss => ss.Set<Customer>().Where(x => x.City == city && x.ContactTitle == contactTitle));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task FromSql_queryable_with_parameters_inline_interpolated(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await AssertQuery(
                    a,
                    ss => ((DbSet<Customer>)ss.Set<Customer>()).FromSql(
                        $"""SELECT * FROM root c WHERE c["City"] = {"London"} AND c["ContactTitle"] = {"Sales Representative"}"""),
                    ss => ss.Set<Customer>().Where(x => x.City == "London" && x.ContactTitle == "Sales Representative"));


            });

    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task Both_FromSql_and_regular_parameters(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                var city = "London";
                var country = "UK";

                await AssertQuery(
                    a,
                    ss => ((DbSet<Customer>)ss.Set<Customer>())
                        .FromSql($"""SELECT * FROM root c WHERE c["City"] = {city} AND c["Country"] = {country}""")
                        .OrderBy(c => c.CustomerID)
                        .Skip(1) // Non-lambda parameter, so becomes a SqlParameter
                        .Take(5),
                    ss => ss.Set<Customer>()
                        .Where(x => x.City == city)
                        .OrderBy(c => c.CustomerID)
                        .Skip(1)
                        .Take(5));


            });


}

internal static class CosmosSqlExtensions
{
    internal static IQueryable<TEntity> FromSqlRaw<TEntity>(this DbSet<TEntity> source, string sql, params object?[] parameters)
        where TEntity : class => CosmosQueryableExtensions.FromSqlRaw(source, sql, parameters);
    internal static IQueryable<TEntity> FromSql<TEntity>(this DbSet<TEntity> source, FormattableString sql)
        where TEntity : class => CosmosQueryableExtensions.FromSql(source, sql);
}

public class CosmosRawSqlFixture : InfoCarrier.Core.CosmosTests.Query.NorthwindFixture<NoopModelCustomizer>
{
    private ITestStoreFactory? _factory;
    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), ContextType, OnModelCreating,
        onAddServices: services => services.AddSingleton<IInfoCarrierRelationalQueryRoots, CosmosSqlQueryRoots>(),
        copyDbContextParameters: (client, server) => ((NorthwindContext)server).TenantPrefix = ((NorthwindContext)client).TenantPrefix,
        configureConventions: ConfigureConventions,
        arbitrarySqlExecution: true,
        allowedTypes: [typeof(CosmosQueryableExtensions)]);

    protected override IServiceCollection AddServices(IServiceCollection services)
        => base.AddServices(services).Replace(ServiceDescriptor.Singleton<IInfoCarrierRelationalQueryRoots, CosmosSqlQueryRoots>());
}
