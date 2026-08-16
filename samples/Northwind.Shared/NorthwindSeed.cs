// Licensed under the MIT license. See license.txt file in the project root for license information.

using Northwind.Shared.Model;

namespace Northwind.Shared;

/// <summary>
///     Deterministic seed data, big enough for a grid to page through and small enough to create in
///     a second.
/// </summary>
/// <remarks>
///     <para>
///         <b>Deterministic without <see cref="Random" />.</b> Every generated value comes from the
///         row's own index through fixed arithmetic, so the data is identical on every machine and
///         every run — which matters because <c>InfoCarrier.Core.TransportTests</c> asserts exact
///         counts against it. A seeded <see cref="Random" /> would be reproducible in practice but
///         would tie those assertions to a runtime implementation detail.
///     </para>
///     <para>
///         <b>The first rows are fixed anchors and must not move.</b> Customers ALFKI/ANATR/AROUT/
///         BERGS, orders 1–5, their seven order lines, and products 1–6 are exactly what they were
///         when the sample had nothing else, because the transport tests address them by identity:
///         order 1 has two lines with quantities 12 and 3, order 1 belongs to ALFKI, product 1 is
///         Chai. Everything generated below starts after them.
///     </para>
/// </remarks>
public static class NorthwindSeed
{
    /// <summary>How many orders the store holds in total, anchors included.</summary>
    public const int OrderCount = 240;

    private static readonly (string Name, decimal UnitPrice, int UnitsInStock, int CategoryId)[] ProductRows =
    [
        // 1–6 are anchors: the tests and the Transfer page name product 1 by id and price.
        ("Chai", 18.00m, 39, 1),
        ("Chang", 19.00m, 17, 1),
        ("Aniseed Syrup", 10.00m, 13, 2),
        ("Chef Anton's Cajun Seasoning", 22.00m, 53, 2),
        ("Pavlova", 17.45m, 29, 3),
        ("Teatime Chocolate Biscuits", 9.20m, 25, 3),

        ("Guaraná Fantástica", 4.50m, 20, 1),
        ("Côte de Blaye", 263.50m, 17, 1),
        ("Chartreuse verte", 18.00m, 69, 1),
        ("Genen Shouyu", 15.50m, 39, 2),
        ("Grandma's Boysenberry Spread", 25.00m, 120, 2),
        ("Northwoods Cranberry Sauce", 40.00m, 6, 2),
        ("Sir Rodney's Marmalade", 81.00m, 40, 3),
        ("Gumbär Gummibärchen", 31.23m, 15, 3),
        ("Schoggi Schokolade", 43.90m, 49, 3),
        ("Queso Cabrales", 21.00m, 22, 4),
        ("Mozzarella di Giovanni", 34.80m, 14, 4),
        ("Gorgonzola Telino", 12.50m, 0, 4),
        ("Singaporean Fried Mee", 14.00m, 26, 5),
        ("Gnocchi di nonna Alice", 38.00m, 21, 5),
        ("Tunnbröd", 9.00m, 61, 5),
        ("Alice Mutton", 39.00m, 0, 6),
        ("Thüringer Rostbratwurst", 123.79m, 0, 6),
        ("Pâté chinois", 24.00m, 115, 6),
        ("Uncle Bob's Organic Dried Pears", 30.00m, 15, 7),
        ("Tofu", 23.25m, 35, 7),
        ("Manjimup Dried Apples", 53.00m, 20, 7),
        ("Ikura", 31.00m, 31, 8),
        ("Konbu", 6.00m, 24, 8),
        ("Carnarvon Tigers", 62.50m, 42, 8),
    ];

    private static readonly (string Id, string CompanyName, string City, string Country)[] CustomerRows =
    [
        // The four anchors, unchanged.
        ("ALFKI", "Alfreds Futterkiste", "Berlin", "Germany"),
        ("ANATR", "Ana Trujillo Emparedados", "México D.F.", "Mexico"),
        ("AROUT", "Around the Horn", "London", "UK"),
        ("BERGS", "Berglunds snabbköp", "Luleå", "Sweden"),

        ("BLAUS", "Blauer See Delikatessen", "Mannheim", "Germany"),
        ("BLONP", "Blondel père et fils", "Strasbourg", "France"),
        ("BOLID", "Bólido Comidas preparadas", "Madrid", "Spain"),
        ("BONAP", "Bon app'", "Marseille", "France"),
        ("BOTTM", "Bottom-Dollar Markets", "Tsawwassen", "Canada"),
        ("BSBEV", "B's Beverages", "London", "UK"),
        ("CACTU", "Cactus Comidas para llevar", "Buenos Aires", "Argentina"),
        ("CENTC", "Centro comercial Moctezuma", "México D.F.", "Mexico"),
        ("CHOPS", "Chop-suey Chinese", "Bern", "Switzerland"),
        ("COMMI", "Comércio Mineiro", "São Paulo", "Brazil"),
        ("CONSH", "Consolidated Holdings", "London", "UK"),
        ("DRACD", "Drachenblut Delikatessen", "Aachen", "Germany"),
        ("DUMON", "Du monde entier", "Nantes", "France"),
        ("EASTC", "Eastern Connection", "London", "UK"),
        ("ERNSH", "Ernst Handel", "Graz", "Austria"),
        ("FAMIA", "Familia Arquibaldo", "São Paulo", "Brazil"),
        ("FISSA", "FISSA Fabrica Inter. Salchichas", "Madrid", "Spain"),
        ("FOLIG", "Folies gourmandes", "Lille", "France"),
        ("FOLKO", "Folk och fä HB", "Bräcke", "Sweden"),
        ("FRANK", "Frankenversand", "München", "Germany"),
        ("FRANR", "France restauration", "Nantes", "France"),
        ("FRANS", "Franchi S.p.A.", "Torino", "Italy"),
        ("FURIB", "Furia Bacalhau e Frutos do Mar", "Lisboa", "Portugal"),
        ("GALED", "Galería del gastrónomo", "Barcelona", "Spain"),
        ("GODOS", "Godos Cocina Típica", "Sevilla", "Spain"),
        ("GOURL", "Gourmet Lanchonetes", "Campinas", "Brazil"),
        ("GREAL", "Great Lakes Food Market", "Eugene", "USA"),
        ("GROSR", "GROSELLA-Restaurante", "Caracas", "Venezuela"),
        ("HANAR", "Hanari Carnes", "Rio de Janeiro", "Brazil"),
        ("HILAA", "HILARIÓN-Abastos", "San Cristóbal", "Venezuela"),
        ("HUNGC", "Hungry Coyote Import Store", "Elgin", "USA"),
        ("ISLAT", "Island Trading", "Cowes", "UK"),
        ("KOENE", "Königlich Essen", "Brandenburg", "Germany"),
        ("LACOR", "La corne d'abondance", "Versailles", "France"),
        ("LAMAI", "La maison d'Asie", "Toulouse", "France"),
        ("LAUGB", "Laughing Bacchus Wine Cellars", "Vancouver", "Canada"),
        ("LAZYK", "Lazy K Kountry Store", "Walla Walla", "USA"),
        ("LEHMS", "Lehmanns Marktstand", "Frankfurt a.M.", "Germany"),
        ("LETSS", "Let's Stop N Shop", "San Francisco", "USA"),
        ("LILAS", "LILA-Supermercado", "Barquisimeto", "Venezuela"),
        ("LINOD", "LINO-Delicateses", "I. de Margarita", "Venezuela"),
        ("LONEP", "Lonesome Pine Restaurant", "Portland", "USA"),
        ("MAGAA", "Magazzini Alimentari Riuniti", "Bergamo", "Italy"),
        ("MEREP", "Mère Paillarde", "Montréal", "Canada"),
        ("OCEAN", "Océano Atlántico Ltda.", "Buenos Aires", "Argentina"),
        ("OTTIK", "Ottilies Käseladen", "Köln", "Germany"),
        ("PICCO", "Piccolo und mehr", "Salzburg", "Austria"),
        ("QUEDE", "Que Delícia", "Rio de Janeiro", "Brazil"),
        ("RANCH", "Rancho grande", "Buenos Aires", "Argentina"),
        ("RICAR", "Ricardo Adocicados", "Rio de Janeiro", "Brazil"),
        ("SEVES", "Seven Seas Imports", "London", "UK"),
        ("SIMOB", "Simons bistro", "København", "Denmark"),
        ("SPECD", "Spécialités du monde", "Paris", "France"),
        ("SUPRD", "Suprêmes délices", "Charleroi", "Belgium"),
        ("THEBI", "The Big Cheese", "Portland", "USA"),
        ("TRADH", "Tradição Hipermercados", "São Paulo", "Brazil"),
        ("VAFFE", "Vaffeljernet", "Århus", "Denmark"),
        ("WANDK", "Die Wandernde Kuh", "Stuttgart", "Germany"),
        ("WELLI", "Wellington Importadora", "Resende", "Brazil"),
        ("WILMK", "Wilman Kala", "Helsinki", "Finland"),
        ("WOLZA", "Wolski Zajazd", "Warszawa", "Poland"),
    ];

    private static readonly string[] CategoryNames =
    [
        "Beverages", "Condiments", "Confections", "Dairy Products",
        "Grains/Cereals", "Meat/Poultry", "Produce", "Seafood",
    ];

    /// <summary>
    ///     Fills an empty store. Does nothing if it already holds customers.
    /// </summary>
    public static void Seed(NorthwindContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Customers.Any())
        {
            return;
        }

        for (int i = 0; i < CategoryNames.Length; i++)
        {
            context.Categories.Add(new Category { Id = i + 1, Name = CategoryNames[i] });
        }

        for (int i = 0; i < ProductRows.Length; i++)
        {
            (string name, decimal price, int stock, int categoryId) = ProductRows[i];
            context.Products.Add(new Product
            {
                Id = i + 1,
                Name = name,
                UnitPrice = price,
                UnitsInStock = stock,
                CategoryId = categoryId,
            });
        }

        foreach ((string id, string company, string city, string country) in CustomerRows)
        {
            context.Customers.Add(new Customer
            {
                Id = id,
                CompanyName = company,
                City = city,
                Country = country,
            });
        }

        // --- the five anchor orders, exactly as they have always been -----------------------
        context.Orders.AddRange(
            new Order { Id = 1, CustomerId = "ALFKI", OrderDate = new DateTime(2026, 1, 5) },
            new Order { Id = 2, CustomerId = "ALFKI", OrderDate = new DateTime(2026, 2, 11) },
            new Order { Id = 3, CustomerId = "ANATR", OrderDate = new DateTime(2026, 2, 18) },
            new Order { Id = 4, CustomerId = "AROUT", OrderDate = new DateTime(2026, 3, 2) },
            new Order { Id = 5, CustomerId = "BERGS", OrderDate = new DateTime(2026, 3, 20) });

        context.OrderDetails.AddRange(
            new OrderDetail { OrderId = 1, ProductId = 1, UnitPrice = 18.00m, Quantity = 12 },
            new OrderDetail { OrderId = 1, ProductId = 5, UnitPrice = 17.45m, Quantity = 3 },
            new OrderDetail { OrderId = 2, ProductId = 2, UnitPrice = 19.00m, Quantity = 5 },
            new OrderDetail { OrderId = 3, ProductId = 3, UnitPrice = 10.00m, Quantity = 20 },
            new OrderDetail { OrderId = 4, ProductId = 4, UnitPrice = 22.00m, Quantity = 7 },
            new OrderDetail { OrderId = 4, ProductId = 6, UnitPrice = 9.20m, Quantity = 10 },
            new OrderDetail { OrderId = 5, ProductId = 1, UnitPrice = 18.00m, Quantity = 2 });

        // --- and the rest, generated from the index ----------------------------------------
        //
        // The multipliers are coprime with the row counts so the spread looks unpatterned: customers
        // advance by 7 of 66, products by 5 of 30, and the second and third line of an order step a
        // further 11 and 22 — which are distinct modulo 30, so no order can name a product twice and
        // break the (OrderId, ProductId) primary key.
        var firstDay = new DateTime(2026, 1, 1);

        for (int id = 6; id <= OrderCount; id++)
        {
            context.Orders.Add(new Order
            {
                Id = id,
                CustomerId = CustomerRows[id * 7 % CustomerRows.Length].Id,
                OrderDate = firstDay.AddDays(id * 3 % 350),
            });

            int lineCount = 1 + (id % 3);
            for (int line = 0; line < lineCount; line++)
            {
                int productIndex = (id * 5 + line * 11) % ProductRows.Length;

                context.OrderDetails.Add(new OrderDetail
                {
                    OrderId = id,
                    ProductId = productIndex + 1,
                    UnitPrice = ProductRows[productIndex].UnitPrice,
                    Quantity = 1 + ((id * 3 + line * 7) % 30),
                });
            }
        }

        context.SaveChanges();
    }
}
