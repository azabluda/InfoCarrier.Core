// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The <em>server-side</em> Northwind context: the shared model plus the InMemory defining
///     queries for the keyless entity types.
/// </summary>
/// <remarks>
///     <para>
///         A keyless entity type has no table of its own; a provider needs a defining query to
///         produce its rows. EF supplies these in <c>NorthwindInMemoryContext</c>, which lives in
///         EF's own InMemory functional-test project and ships in no package we reference — so
///         the same definitions are restated here. Without them the server's keyless sets are
///         simply empty, which is what "Expected: 91, Actual: 0" across
///         <c>NorthwindKeylessEntitiesQuery</c> was reporting.
///     </para>
///     <para>
///         This is deliberately the <em>server</em> context only. A defining query is how the
///         backing store produces rows, which is exactly the kind of thing a client remoting over
///         the wire has no business knowing: the client model needs the keyless entity types (it
///         gets them from the fixture's shared <c>OnModelCreating</c>) and nothing more.
///     </para>
/// </remarks>
public class NorthwindInfoCarrierServerContext(DbContextOptions options) : NorthwindContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CustomerQuery>().ToInMemoryQuery(() => Customers.Select(c => new CustomerQuery
        {
            Address = c.Address,
            City = c.City,
            CompanyName = c.CompanyName,
            ContactName = c.ContactName,
            ContactTitle = c.ContactTitle,
        }));

        modelBuilder.Entity<OrderQuery>().ToInMemoryQuery(
            () => Orders.Select(o => new OrderQuery { CustomerID = o.CustomerID }));

        modelBuilder.Entity<ProductQuery>().ToInMemoryQuery(() => Products.Where(p => !p.Discontinued)
            .Select(p => new ProductQuery
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                CategoryName = "Food",
            }));

        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToInMemoryQuery(
            () => Customers.Select(c => new CustomerQueryWithQueryFilter
            {
                CompanyName = c.CompanyName,
                OrderCount = c.Orders.Count(),
                SearchTerm = SearchTerm,
            }));
    }
}
