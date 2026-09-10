// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
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
    ///     The refusal the switch exists to lift, against a store that genuinely answers it.
    /// </summary>
    /// <remarks>
    ///     <b>By default the client refuses a <c>Distinct</c> over a projection carrying a
    ///     collection</b>, because every relational provider does: the identifying columns do not
    ///     survive it. A document store has no such problem. This asserts the two directions
    ///     differ, which is the switch's entire premise and the thing InMemory could not show.
    /// </remarks>
    [Fact]
    public async Task Distinct_over_a_collection_projection_is_refused_by_default_and_allowed_when_told()
    {
        await using (ShopClientContext relational = fixture.CreateClient())
        {
            await Assert.ThrowsAnyAsync<Exception>(
                () => relational.Customers
                    .Select(c => new { c.Name, c.Lines })
                    .Distinct()
                    .ToListAsync());
        }

        await using ShopClientContext document = fixture.CreateNonRelationalClient();
        var rows = await document.Customers
            .Select(c => new { c.Name, c.Lines })
            .Distinct()
            .ToListAsync();

        Assert.Equal(3, rows.Count);
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
