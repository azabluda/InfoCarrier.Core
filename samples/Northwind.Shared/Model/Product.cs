namespace Northwind.Shared.Model;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int UnitsInStock { get; set; }

    public int CategoryId { get; set; }
}
