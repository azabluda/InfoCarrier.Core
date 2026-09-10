// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     The same writes, run directly against the server with InfoCarrier out of the picture.
/// </summary>
/// <remarks>
///     <para>
///         <b>THIS CLASS IS A CONTROL AND NOTHING ELSE.</b> It tests EF Core and the MongoDB
///         provider, neither of which is this repository's code, and it earns its place only by
///         answering a question no other test can: when an update through the wire fails, is the
///         wire at fault or is the store?
///     </para>
///     <para>
///         <b>It exists because the tier's first run failed three update tests</b> with
///         "the entity of type 'Address' is mapped as a part of the document mapped to 'Customer',
///         but there is no tracked entity of this type with the corresponding key value", and one
///         with "Field 'Address' required but not present in BsonDocument". Those messages are
///         about owned entities and document shape, which is precisely the seam where this
///         provider and a document store could each plausibly be wrong. Guessing which would have
///         been the same mistake as reporting a Mongo failure that turned out to be an EF version
///         defect, which happened once already.
///     </para>
///     <para>
///         <b>Read it as a fork.</b> If these pass and the wire tests fail, the fault is on this
///         side of the wire. If these fail too, the fault is below us and the wire tests are
///         describing somebody else's behaviour.
///     </para>
/// </remarks>
public class ServerSideControlTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    private ShopServerContext Server(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ShopServerContext>();

    [Fact]
    public async Task Server_side_scalar_update_keeps_the_nested_document()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer bob = await db.Customers.SingleAsync(c => c.Id == "bob");
            bob.Name = "Robert";
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer bob = await db.Customers.SingleAsync(c => c.Id == "bob");

            Assert.Equal("Robert", bob.Name);
            Assert.Equal("Lisbon", bob.Address.City);
        }
    }

    [Fact]
    public async Task Server_side_nested_document_update_persists()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer alice = await db.Customers.SingleAsync(c => c.Id == "alice");
            alice.Address.City = "Munich";
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Assert.Equal("Munich", (await db.Customers.SingleAsync(c => c.Id == "alice")).Address.City);
        }
    }

    [Fact]
    public async Task Server_side_nested_array_append_persists()
    {
        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer carol = await db.Customers.SingleAsync(c => c.Id == "carol");
            carol.Lines.Add(new OrderLine { Sku = "chair", Quantity = 4 });
            await db.SaveChangesAsync();
        }

        using (IServiceScope scope = fixture.ServerProvider.CreateScope())
        {
            ShopServerContext db = Server(scope);
            Customer carol = await db.Customers.SingleAsync(c => c.Id == "carol");

            Assert.Equal("chair", Assert.Single(carol.Lines).Sku);
        }
    }
}
