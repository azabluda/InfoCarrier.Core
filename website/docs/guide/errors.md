# Handling errors

Two things can go wrong that would not go wrong locally: the server can fail, and the journey can
fail. They are deliberately different exception types, because the response to each is different.

## A failure the server reported

The server's exception is carried back as data and raised again on the client, keeping its type,
its message and its inner chain. You catch what you would catch locally.

```csharp
try
{
    await context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    // Someone else changed the row.
}
catch (DbUpdateException ex)
{
    // A constraint, a trigger, a server-side validation.
}
```

Deeper in the chain, where the original exception is a type your client has no reason to reference,
such as a `SqliteException`, it arrives as `InfoCarrierServerException`. The name of what the server
threw is a property rather than the type, so a `catch` clause never obliges a client to reference a
database driver.

```csharp
catch (DbUpdateException ex) when (ex.InnerException is InfoCarrierServerException server)
{
    logger.LogError(
        "server threw {Type}: {Message}",
        server.ServerExceptionTypeName,     // e.g. "Microsoft.Data.Sqlite.SqliteException"
        server.Message);
}
```

A real chain from a foreign-key violation looks like this:

```text
DbUpdateException                     the client's own, from EF
  └── DbUpdateException               the server's, rebuilt with its message
      └── InfoCarrierServerException  ServerExceptionTypeName = "Microsoft.Data.Sqlite.SqliteException"
```

The server's stack trace is preserved, and deliberately not spliced into the client-side
exception's own stack, which would misreport where your client is. It travels in `Exception.Data`,
and it is the only view you get of what happened on the other side, so log it.

```csharp
var serverStack = ex.InnerException?.Data["InfoCarrier.ServerStackTrace"] as string;
```

## A failure of the journey

If the request never reached a server, or what came back was not a valid response, you get
`InfoCarrierTransportException`. This is not a database error and must not be handled as one: the
data is unknown, not wrong, and retrying may work.

```csharp
try
{
    List<Customer> customers = await context.Customers.ToListAsync();
}
catch (InfoCarrierTransportException ex)
{
    // Offline, DNS, TLS, a 502 from a proxy, a captive portal returning HTML.
    logger.LogWarning(ex, "the application server could not be reached");
}
```

The underlying failure, an `HttpRequestException` or a serialization error, is kept as
`InnerException`, because it names the layer that actually failed. When the server answers with a
non-success status, the message carries the status code and the response body.

Depending on where the failure surfaces, EF may wrap the transport exception, so check both:

```csharp
catch (Exception ex) when (ex is InfoCarrierTransportException
                           || ex.InnerException is InfoCarrierTransportException)
```

## Which is which

| Exception | Meaning | Sensible response |
|---|---|---|
| `DbUpdateException`, `DbUpdateConcurrencyException` | The server ran your work and the database refused it | Handle as you would locally |
| `InvalidOperationException` | The query could not be translated, or the model disagrees | Fix the query; do not retry |
| `InfoCarrierServerException` | The server's own exception type is not available here | Log `ServerExceptionTypeName`; treat as its outer type |
| `InfoCarrierTransportException` | The request did not complete | Retry, queue, or tell the user they are offline |

Catch the type, not the message. Message text is not a supported contract on any EF Core provider,
and a couple of messages here are worded differently from other providers'. See
[Limitations](../limitations.md).

## Cancellation

Every async method takes a `CancellationToken` and passes it through to the transport. A cancelled
request raises `OperationCanceledException` and is never reported as a transport failure, because
it is your own signal rather than something that went wrong.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

List<Customer> customers = await context.Customers.ToListAsync(cts.Token);
```

## What the server does not tell you

A fault carries the type name, the message, the inner chain and the stack trace, and nothing else
about the server: no paths beyond what a stack trace contains, no connection strings, no
configuration. If your own exception messages carry information a client should not see, review
that on the server. See [Security](../security.md).
