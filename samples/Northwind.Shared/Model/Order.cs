// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace Northwind.Shared.Model;

public class Order
{
    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    // `virtual`, so a lazy-loading proxy can override it. That is for the SERVER's benefit: the
    // Blazor client deliberately does not enable proxies, because a navigation getter is
    // synchronous and a single-threaded WebAssembly runtime cannot block on the round trip a lazy
    // load needs. The browser uses `Entry(x).Reference(...).LoadAsync()` instead.
    //
    // Left `virtual` because this model is shared by both halves and the server does use it, and
    // because it costs a client nothing: an entity type with virtual navigations materializes
    // perfectly well without proxies.
    public virtual Customer? Customer { get; set; }

    public virtual List<OrderDetail> OrderDetails { get; set; } = [];
}
