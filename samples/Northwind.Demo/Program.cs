// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Northwind.Client.Transport;
using Northwind.Demo;
using Northwind.Shared;
using Northwind.Shared.Model;

// The address of Northwind.Server. Its launch profile pins this port, so both commands are bare.
var baseAddress = new Uri(args.Length > 0 ? args[0] : "http://localhost:5199");

var serializer = new SystemTextJsonInfoCarrierSerializer();
var counter = new CountingHandler(new HttpClientHandler());
using var httpClient = new HttpClient(counter) { BaseAddress = baseAddress };

// This is the whole client wiring, and it is the point of the sample: an HttpClient, the
// transport, and `UseInfoCarrier`. Nothing below configures a database, because there is none.
var client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(httpClient, serializer), serializer);

DbContextOptions<NorthwindContext> options = new DbContextOptionsBuilder<NorthwindContext>()
    .UseInfoCarrier(client)
    .UseLazyLoadingProxies()
    .Options;

Console.WriteLine();
Console.WriteLine("  InfoCarrier.Core — Northwind demo");
Console.WriteLine($"  Server: {baseAddress}");
Console.WriteLine("  This process has no database. Every line below crossed a TCP socket.");
Console.WriteLine();

try
{
    await RunAsync();
}
catch (HttpRequestException exception)
{
    Console.WriteLine("  Could not reach the server.");
    Console.WriteLine($"  {exception.Message}");
    Console.WriteLine();
    Console.WriteLine("  Start it first, in another terminal:");
    Console.WriteLine("      dotnet run --project samples/Northwind.Server");
    return 1;
}

return 0;

async Task RunAsync()
{
    await Step(
        "A filtered query — the Where runs on the server",
        async () =>
        {
            using var context = new NorthwindContext(options);

            List<Customer> customers = await context.Customers
                .Where(c => c.Country == "Germany")
                .OrderBy(c => c.Id)
                .ToListAsync();

            foreach (Customer customer in customers)
            {
                Console.WriteLine($"      {customer.Id}  {customer.CompanyName}  ({customer.City})");
            }
        });

    await Step(
        "A projection — only the selected columns cross the wire",
        async () =>
        {
            using var context = new NorthwindContext(options);

            var rows = await context.Orders
                .OrderBy(o => o.Id)
                .Select(o => new { o.Id, o.CustomerId })
                .ToListAsync();

            Console.WriteLine($"      {rows.Count} orders, as Id + CustomerId pairs:");
            Console.WriteLine("      " + string.Join("  ", rows.Select(r => $"{r.Id}:{r.CustomerId}")));
        });

    await Step(
        "An aggregate — the server answers with a number, not with rows",
        async () =>
        {
            using var context = new NorthwindContext(options);

            int count = await context.OrderDetails.CountAsync(d => d.Quantity >= 10);

            Console.WriteLine($"      order lines with quantity >= 10: {count}");
        });

    await Step(
        "Lazy loading — touching a navigation costs another round trip",
        async () =>
        {
            using var context = new NorthwindContext(options);

            Order order = await context.Orders.SingleAsync(o => o.Id == 1);
            Console.WriteLine($"      order {order.Id} loaded          (round trips so far: {counter.Requests})");

            Customer? customer = order.Customer;
            Console.WriteLine($"      order.Customer -> {customer?.CompanyName}   (round trips so far: {counter.Requests})");

            int lines = order.OrderDetails.Count;
            Console.WriteLine($"      order.OrderDetails -> {lines} lines        (round trips so far: {counter.Requests})");
        });

    await Step(
        "Unit of work — two edits, one SaveChanges, one round trip",
        async () =>
        {
            using var context = new NorthwindContext(options);

            List<OrderDetail> lines = await context.OrderDetails
                .Where(d => d.OrderId == 4)
                .OrderBy(d => d.ProductId)
                .ToListAsync();

            int before = counter.Requests;
            foreach (OrderDetail line in lines)
            {
                line.Quantity++;
            }

            int written = await context.SaveChangesAsync();

            Console.WriteLine(
                $"      {lines.Count} lines edited, {written} rows written, "
                    + $"{counter.Requests - before} round trip for the save");
        });

    await Step(
        "A transaction — rolled back, and the store never sees it",
        async () =>
        {
            using var context = new NorthwindContext(options);

            int before = await context.Products.CountAsync();

            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                context.Products.Add(
                    new Product { Name = "Rolled back", UnitPrice = 1m, UnitsInStock = 1, CategoryId = 1 });

                await context.SaveChangesAsync();

                int inside = await context.Products.CountAsync();
                Console.WriteLine($"      inside the transaction: {inside} products");

                await transaction.RollbackAsync();
            }

            using var verify = new NorthwindContext(options);
            Console.WriteLine($"      after the rollback:     {await verify.Products.CountAsync()} products (was {before})");
        });

    Console.WriteLine();
    Console.WriteLine($"  Done. {counter.Requests} round trips, none of which touched a database in this process.");
    Console.WriteLine();
}

async Task Step(string title, Func<Task> body)
{
    Console.WriteLine($"  {title}");
    await body();
    Console.WriteLine();
}
