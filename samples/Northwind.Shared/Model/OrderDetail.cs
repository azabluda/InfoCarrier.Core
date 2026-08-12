// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace Northwind.Shared.Model;

public class OrderDetail
{
    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public virtual Product? Product { get; set; }
}
