# Handling errors

The server can fail, and so can the trip to it. Those are different exception types, because they
call for different responses.

## A failure the server reported

The server's exception is carried back as data and raised again on the client, with its message and
its inner chain. You catch what you would catch locally, and where the client has the exception's
type loaded it is raised as that type.

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

So a `SqlException`, in a client that references no database driver, arrives as
`InfoCarrierServerException` with the thrown type's name in a property. No `catch` clause is ever
obliged to reference a database driver.

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

The library keeps the server's stack trace and deliberately does not splice it into the
client-side exception's own stack, which would misreport where your client is. It travels in
`Exception.Data`, and it is the only view you get of what happened on the other side, so log it.

```csharp
var serverStack = ex.InnerException?.Data["InfoCarrier.ServerStackTrace"] as string;
```

## A failure of the journey

If the request never reached a server, or what came back was not a valid response, you get
`InfoCarrierTransportException`. This is not a database error and must not be handled as one: the
data is unknown, not wrong.

Retrying a read is safe. **Retrying anything that writes is not**, because the failure does not
tell you whether the server committed: the request may have died on the way out, or the answer may
have died on the way back. Nothing in the envelope carries a request id, so there is no way to ask
afterwards.

That covers more than `SaveChanges`. `ExecuteUpdate` and `ExecuteDelete` are written on an
`IQueryable` and read like queries, and they are writes. For an insert, the remedy is a key you
supply rather than a store-generated one, with a unique constraint on the server's database that
enforces it. Without the constraint a supplied key is not idempotent and the retry simply inserts
twice.

**An update has no such remedy here.** A retried `balance = balance - 100` debits twice, and nothing
in the protocol can tell you whether the first one landed. Make the operation one whose repetition
is harmless, such as setting a value rather than adjusting one, or carry your own applied-once
marker in the row and check it on the server. Reading back before you retry is not equivalent: the
first write can still commit between your read and your retry.

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
`InnerException`. When the server answers with a non-success status, the message carries the status
code and the response body.

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

A fault carries the type name, the message, the inner chain and the stack trace. The library adds
nothing to that: no configuration, no connection string, no server path beyond whatever the stack
trace itself holds, which with symbols deployed is your build's source paths.

**Your own messages travel verbatim**, and a provider's exception is your own message here. A
`SqlException` names the server instance and the database in its text, and that text reaches the
client. Catch and rewrite at the server boundary anything a client should not read. See
[Security](../security.md).

That resolution is also the bound on the return direction: a fault cannot make the client load an
assembly, or construct anything that is not an exception.
