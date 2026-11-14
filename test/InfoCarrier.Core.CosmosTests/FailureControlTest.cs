// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class FailureControlTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public async Task Explicit_transaction_refusal_matches_the_direct_store()
    {
        await using ShopServerContext direct = fixture.CreateBackend();
        Exception? expected = await Record.ExceptionAsync(async () =>
            await direct.Database.BeginTransactionAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(expected);
        await using ShopClientContext wire = fixture.CreateNonRelationalClient();
        Exception? actual = await Record.ExceptionAsync(async () =>
            await wire.Database.BeginTransactionAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
    }

    [Fact]
    public async Task Updating_a_missing_document_preserves_the_direct_store_failure()
    {
        string id = Guid.NewGuid().ToString("N");
        await using ShopServerContext direct = fixture.CreateBackend();
        var missingDirect = new Customer { Id = id, Name = "Missing", Country = "DE" };
        direct.Attach(missingDirect);
        direct.Entry(missingDirect).Property(c => c.Name).IsModified = true;
        DbUpdateException expected = await Assert.ThrowsAsync<DbUpdateException>(() => direct.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(System.Net.HttpStatusCode.NotFound,
            Assert.IsType<Microsoft.Azure.Cosmos.CosmosException>(expected.InnerException).StatusCode);
        await using ShopClientContext wire = fixture.CreateNonRelationalClient();
        var missingWire = new Customer { Id = id, Name = "Missing", Country = "DE" };
        wire.Attach(missingWire);
        wire.Entry(missingWire).Property(c => c.Name).IsModified = true;
        DbUpdateException actual = await Assert.ThrowsAsync<DbUpdateException>(
            () => wire.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expected.Message, actual.Message);
        DbUpdateException remote = Assert.IsType<DbUpdateException>(actual.InnerException);
        Assert.Equal(expected.Message, remote.Message);
        Assert.Contains("404", Assert.IsType<InfoCarrierServerException>(remote.InnerException).Message, StringComparison.Ordinal);
        Assert.Same(missingWire, Assert.Single(actual.Entries).Entity);
        Assert.Equal(EntityState.Modified, wire.Entry(missingWire).State);
        await using ShopServerContext read = fixture.CreateBackend();
        Assert.False(await read.Customers.AnyAsync(c => c.Id == id, TestContext.Current.CancellationToken));
    }
}
