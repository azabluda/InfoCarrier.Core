// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     The one model this tier uses, and it is shaped like a document on purpose.
/// </summary>
/// <remarks>
///     <para>
///         <b>A nested object AND a nested array</b>, because that is the shape a relational store
///         cannot hold without joining. <c>Address</c> is one document inside another;
///         <c>Lines</c> is an array inside one. Against SQLite these become tables and a join;
///         against MongoDB they are simply there, and no join exists to get them wrong.
///     </para>
///     <para>
///         <b>THE KEY IS A STRING, AND THAT IS A FINDING RATHER THAN A PREFERENCE.</b> MongoDB's
///         natural key type is <c>ObjectId</c>, and the CLIENT cannot map it: the client has no
///         MongoDB provider, so a Mongo-native type is not in its model at all and the shared model
///         fails to build. A key type both halves understand is therefore a REQUIREMENT of this
///         architecture against a document store, not a convenience, and it was found by trying.
///     </para>
/// </remarks>
public class Customer
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Country { get; set; } = null!;

    /// <summary>A document inside a document.</summary>
    public Address Address { get; set; } = null!;

    /// <summary>An array inside a document.</summary>
    public List<OrderLine> Lines { get; set; } = [];
}

/// <summary>Nested, and never a table of its own on this store.</summary>
public class Address
{
    public string City { get; set; } = null!;

    public string Postcode { get; set; } = null!;
}

/// <summary>One element of the nested array.</summary>
public class OrderLine
{
    public string Sku { get; set; } = null!;

    public int Quantity { get; set; }
}

/// <summary>
///     The SERVER's model, which knows it is MongoDB.
/// </summary>
/// <remarks>
///     <c>ToCollection</c> is the Mongo provider's own extension. The client below cannot say it,
///     which is the asymmetry this tier exists to exercise: the two halves describe the same
///     entities and only one of them knows what the store is.
/// </remarks>
public class ShopServerContext(DbContextOptions<ShopServerContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().ToCollection("customers");
        modelBuilder.Entity<Customer>().OwnsOne(c => c.Address);
        modelBuilder.Entity<Customer>().OwnsMany(c => c.Lines);
    }
}

/// <summary>
///     The CLIENT's model, which has no database and no idea what the store is.
/// </summary>
public class ShopClientContext(DbContextOptions<ShopClientContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().OwnsOne(c => c.Address);
        modelBuilder.Entity<Customer>().OwnsMany(c => c.Lines);
    }
}
