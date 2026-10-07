// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     The claim this whole tier exists to test: that this provider is not relational-only.
/// </summary>
/// <remarks>
///     <b>READ-ONLY, AND THAT IS LOAD-BEARING.</b> Tests in one class share one fixture and run in
///     an arbitrary order, so a test asserting an absolute count cannot sit beside one that writes.
///     Three tests here failed exactly that way on the tier's first run. Every write lives in
///     <see cref="DocumentWriteTest" />, which owns its own server and asserts only on rows it
///     created.
/// </remarks>
/// <remarks>
///     <para>
///         <b>`InMemorySmokeTest` already makes this claim and calls its own evidence weak, by
///         name.</b> Its comment reads: "WEAK EVIDENCE ON PURPOSE, and issue #51 says why: InMemory
///         has no nested-document shape and no translation refusals of its own, so it disagrees
///         with a relational store about almost nothing. A document-store tier is what would answer
///         the question properly. This is the cheap half." This class is the other half.
///     </para>
///     <para>
///         <b>`UseNonRelationalServerStore()` is the API under test.</b> It lifts three refusals on
///         the argument that a non-relational store answers those queries. Until this tier existed
///         that argument had never been checked against a store which actually has the property:
///         the only non-relational store in the suite answers everything and refuses nothing.
///     </para>
/// </remarks>
public class NonRelationalStoreTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task The_client_works_without_being_told_the_store_is_not_relational()
    {
        // The default is the relational one, and a document store still answers ordinary queries.
        await using ShopClientContext db = fixture.CreateClient();

        Assert.Equal(3, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task The_client_works_when_told_the_store_is_not_relational()
    {
        await using ShopClientContext db = fixture.CreateNonRelationalClient();

        List<Customer> all = await db.Customers.OrderBy(c => c.Id).ToListAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal("Berlin", all[0].Address.City);
    }

    /// <summary>
    ///     Anonymous collection carriers follow the document store's actual tracking rules.
    /// </summary>
    /// <remarks>
    ///     Before 2026-10-07 this was a tuple reassembly guarded by the non-relational switch.
    ///     A bounded anonymous descriptor reaches MongoDB directly with either client setup.
    ///     Its owned collection requires no tracking when the owner is absent from the result.
    ///     MongoDB also refuses materializing this DISTINCT projection without its document key.
    ///     Compare both refusals with a direct backend context instead of inventing support.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_collection_distinct_follows_backend_tracking_rules(bool nonRelational)
    {
        await using ShopClientContext document = nonRelational
            ? fixture.CreateNonRelationalClient() : fixture.CreateClient();
        var query = document.Customers
            .Select(c => new { c.Name, c.Lines })
            .Distinct();
        InvalidOperationException refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync());
        Assert.Contains("owned entity without a corresponding owner", refusal.Message, StringComparison.Ordinal);
        using IServiceScope scope = fixture.ServerProvider.CreateScope();
        await using ShopServerContext backend = scope.ServiceProvider.GetRequiredService<ShopServerContext>();
        var direct = backend.Customers.Select(c => new { c.Name, c.Lines }).Distinct();
        InvalidOperationException expectedTracking = await Assert.ThrowsAsync<InvalidOperationException>(() => direct.ToListAsync());
        Assert.Equal(expectedTracking.Message, refusal.Message);
        InvalidOperationException expectedMaterialization = await Assert.ThrowsAsync<InvalidOperationException>(
            () => direct.AsNoTracking().ToListAsync());
        InvalidOperationException actualMaterialization = await Assert.ThrowsAsync<InvalidOperationException>(
            () => query.AsNoTracking().ToListAsync());
        Assert.Equal(expectedMaterialization.Message, actualMaterialization.Message);
        Assert.Contains("Document element is missing", actualMaterialization.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The relational model is never built, on a store where it would mean nothing.
    /// </summary>
    /// <remarks>
    ///     <b>The other half of the claim, and the one that would catch a regression.</b> Since V5
    ///     the client CAN build EF's relational model and puts a lazy factory on every model to do
    ///     it. If some future code path started forcing that factory, every query against every
    ///     store would pay for it, including one with no tables at all. `InMemorySmokeTest` pins
    ///     this against a store that merely has no tables; this pins it against one whose documents
    ///     are not tables even in principle.
    /// </remarks>
    [Fact]
    public async Task Ordinary_use_never_builds_the_relational_model()
    {
        await using ShopClientContext db = fixture.CreateNonRelationalClient();

        Assert.Single(await db.Customers.Where(c => c.Address.City == "Berlin").ToListAsync());

        Assert.NotNull(db.Model.FindRuntimeAnnotation("Relational:RelationalModelFactory"));
        Assert.Null(db.Model.FindRuntimeAnnotation("Relational:RelationalModel"));
    }
}
