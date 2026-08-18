# Handling errors

Two things can go wrong that would not go wrong locally: the **server** can fail, and the **journey**
can fail. They are deliberately different exception types, because the response to each is
different.

## A failure the server reported

The server's exception is carried back as data and raised again on the client, keeping its **type**,
its **message** and its **inner chain**. So you catch what you would catch locally:

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

Deeper in the chain, where the original exception is a type your client has no reason to reference
— a `SqliteException`, a `SqlException`, a driver's own type — it arrives as
`InfoCarrierServerException`, which still names what the server actually threw:

```csharp
catch (DbUpdateException ex) when (ex.InnerException is InfoCarrierServerException server)
{
    logger.LogError(
        "server threw {Type}: {Message}",
        server.ServerExceptionTypeName,     // e.g. "Microsoft.Data.Sqlite.SqliteException"
        server.Message);
}
```

Making every client reference every database driver, just so a `catch` clause could name the type,
would be a worse trade than losing the name from the clause — so the name lives in a property
instead.

A real chain from a foreign-key violation looks like this:

```text
DbUpdateException                     the client's own, from EF
  └── DbUpdateException               the server's, rebuilt with its message
      └── InfoCarrierServerException  ServerExceptionTypeName = "Microsoft.Data.Sqlite.SqliteException"
```

### The server's stack trace

It is preserved, and deliberately not spliced into the client-side exception's own stack — that
would be lying about where your client is. It travels in `Exception.Data`:

```csharp
var serverStack = ex.InnerException?.Data["InfoCarrier.ServerStackTrace"] as string;
```

Log it. It is the only view you get of what happened on the other side.

## A failure of the journey

If the request never reached a server, or what came back was not a valid response, you get
`InfoCarrierTransportException` instead. This is not a database error and must not be handled as
one:

```csharp
try
{
    List<Customer> customers = await context.Customers.ToListAsync();
}
catch (InfoCarrierTransportException ex)
{
    // Offline, DNS, TLS, a 502 from a proxy, a captive portal returning HTML.
    // Retrying may work. The data is unknown, not wrong.
    logger.LogWarning(ex, "the application server could not be reached");
}
```

The underlying failure — an `HttpRequestException`, a serialization error — is kept as
`InnerException`, because it names the layer that actually failed. When the server answers with a
non-success status, the message carries the status code **and the response body**: a bare status
code is indistinguishable from a dozen unrelated causes to whoever has to diagnose it.

Depending on where the failure surfaces, the transport exception may be wrapped by EF, so check
both:

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

## Do not match on message text

Where a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does — but the wording differs from other providers' in a couple of cases. Message text is not
a supported contract on **any** EF Core provider; catch the type. See
[Limitations](../limitations.md).

## Cancellation

Every async method takes a `CancellationToken` and passes it through to the transport. A cancelled
request raises `OperationCanceledException` and is never reported as a transport failure — it is
your own signal, not something that went wrong.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

List<Customer> customers = await context.Customers.ToListAsync(cts.Token);
```

## What the server does not tell you

Where the server maps a failure into a fault, it sends the type name, the message, the inner chain
and the stack trace. It does not send anything else about itself — no server paths beyond what a
stack trace contains, no connection strings, no configuration. If your exception messages carry
information a client should not see, that is worth reviewing on the server: see
[Security](../security.md).
