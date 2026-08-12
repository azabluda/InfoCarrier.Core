// Licensed under the MIT license. See license.txt file in the project root for license information.

using Northwind.Shared.Model;

namespace Northwind.Shared;

/// <summary>
///     Deterministic seed data. Small on purpose: the sample demonstrates a wire protocol, not
///     a data set.
/// </summary>
public static class NorthwindSeed
{
    public static void Seed(NorthwindContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Customers.Any())
        {
            return;
        }

        context.Categories.AddRange(
            new Category { Id = 1, Name = "Beverages" },
            new Category { Id = 2, Name = "Condiments" },
            new Category { Id = 3, Name = "Confections" });

        context.Products.AddRange(
            new Product { Id = 1, Name = "Chai", UnitPrice = 18.00m, UnitsInStock = 39, CategoryId = 1 },
            new Product { Id = 2, Name = "Chang", UnitPrice = 19.00m, UnitsInStock = 17, CategoryId = 1 },
            new Product { Id = 3, Name = "Aniseed Syrup", UnitPrice = 10.00m, UnitsInStock = 13, CategoryId = 2 },
            new Product { Id = 4, Name = "Chef Anton's Cajun Seasoning", UnitPrice = 22.00m, UnitsInStock = 53, CategoryId = 2 },
            new Product { Id = 5, Name = "Pavlova", UnitPrice = 17.45m, UnitsInStock = 29, CategoryId = 3 },
            new Product { Id = 6, Name = "Teatime Chocolate Biscuits", UnitPrice = 9.20m, UnitsInStock = 25, CategoryId = 3 });

        context.Customers.AddRange(
            new Customer { Id = "ALFKI", CompanyName = "Alfreds Futterkiste", City = "Berlin", Country = "Germany" },
            new Customer { Id = "ANATR", CompanyName = "Ana Trujillo Emparedados", City = "México D.F.", Country = "Mexico" },
            new Customer { Id = "AROUT", CompanyName = "Around the Horn", City = "London", Country = "UK" },
            new Customer { Id = "BERGS", CompanyName = "Berglunds snabbköp", City = "Luleå", Country = "Sweden" });

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

        context.SaveChanges();
    }
}
