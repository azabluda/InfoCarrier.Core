# Stream query results

Asynchronous, non-tracking queries yield rows while the server is still reading the result. Use
`AsAsyncEnumerable()` when the caller can process one row at a time:

```csharp
await foreach (Order order in context.Orders
    .AsNoTracking()
    .Where(order => order.Freight > 50m)
    .AsAsyncEnumerable())
{
    Console.WriteLine(order.Id);
}
```

Stopping enumeration disposes the response, cancels the server producer, and releases operation
resources. An open transaction keeps its context until that transaction ends.
`ToListAsync()` still receives the streamed rows, but the list itself is complete
only after enumeration finishes.

The client can reconstruct projections incrementally after a query split. A concatenated query
also preserves progressive delivery. Operations that need the complete input remain buffered.

Tracked queries are buffered before client materialization. Entity identity and navigation fix-up
can depend on later rows, so the client preserves the complete result graph before yielding it.
Single-result operations are buffered for the same conservative boundary. Use `AsNoTracking()`
for read-only queries that should stream.

## Limits and late failures

A stream can fail after returning rows. Treat an unfinished result as partial.
Do not publish a complete report or update progress as complete until enumeration finishes.
A request cancelled by the caller raises cancellation; other errors follow the [error guide](../operations/errors.md).

The client's response limit counts the entire streamed response, including message metadata.
Set it on that client registration. A limit can be exceeded after earlier rows were processed.
The server independently limits the incoming request.
See [options and limits](../reference/options.md).

A custom server without `IInfoCarrierStreamServer` is adapted from its buffered result.
A custom streaming transport uses `IInfoCarrierStreamTransport`.
Custom processing that needs the complete input can also require buffering.
See [custom transports](../extending/transports.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
