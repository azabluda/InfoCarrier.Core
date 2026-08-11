namespace Northwind.Shared.Model;

public class Order
{
    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    // `virtual`, so the proxy can override it. Automatic lazy loading is the target; if it turns
    // out not to work in the browser, spec 3.2 records the ILazyLoader fallback and it is a
    // change to this folder only.
    public virtual Customer? Customer { get; set; }

    public virtual List<OrderDetail> OrderDetails { get; set; } = [];
}
