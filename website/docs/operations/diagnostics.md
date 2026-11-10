# Diagnose requests and execution

Start by checking whether the server reported a failure or the connection failed.
The [error guide](errors.md) lists the exception types.
For a lost write response, follow [write recovery](write-recovery.md) before retrying.

## Start with the symptom

| Symptom | First check |
|---|---|
| A query throws | Read the query failure and exception chain before changing registration or translation. |
| A save reports a constraint or concurrency failure | Inspect the server-reported exception. Treat a connection failure separately. |
| A save response is lost | Follow [write recovery](write-recovery.md); do not resend it automatically. |
| A screen is slow or sends many requests | Count client requests, then inspect queries and navigation loads. |
| Several endpoints behave differently | Check registration names and labels on both client and server. |

## Log client execution

```csharp
var options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .Options;
```

Import `Microsoft.Extensions.Logging`.
Use a fresh context for each independent operation.
Avoid logging sensitive query values in production.

| Event | What it tells you |
|---|---|
| `InfoCarrierEventId.QuerySplit` | Part of the final result is reconstructed on the client. The event describes the split and server-query count. |
| `InfoCarrierEventId.ClientQueryFailure` | A client query failed. The message identifies the phase, outcome, and exception type. |
| `InfoCarrierEventId.ClientSaveFailure` | A save failed on the client path. Check its exception chain before choosing recovery. |

Use `ConfigureWarnings` and normal Entity Framework logging to select events and severity.
A query split does not authorize silent local filtering, ordering, grouping, joins, or equality.
See [query execution](../applications/querying.md).

## Sending the server's log to the client

EF writes its warnings about a query or a save on the server, so a client never sees them.
Forwarding is off until you grant it. Then they arrive in the client's own logger, under the
server's category and event id:

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>(options =>
    options.WithLogForwarding(LogLevel.Warning));
```

Pass another `LogLevel` to change it. Pass `null` to disable forwarding. Each call resets the
sensitive-data grant unless `includeSensitiveData: true` is supplied. At `Information`, every server SQL command
goes too,
which tells a client your schema.

A server whose context enables sensitive data logging forwards nothing unless
`LogForwarding.IncludeSensitiveData` is also `true`. That setting changes what EF's messages say
everywhere, so no rule can identify only messages carrying values.

Model and context events never cross. Both halves build a model, and each logs its own.

## Counting round trips

The cost of this provider is round trips. To count them in a running application, listen to the
meter named `InfoCarrier.Core`:

```bash
dotnet-counters monitor --process-id <pid> --counters InfoCarrier.Core
```

`infocarrier.client.round_trips` counts completed attempts, including failures before sending.
`infocarrier.client.round_trip.duration` records seconds through response disposal. Streamed queries
include enumeration time. Both carry `infocarrier.operation` and `infocarrier.outcome`: `success`,
`error`, `cancelled`, or `incomplete` when enumeration stops before completion. Failures carry `error.type`.
Fault tags describe a loaded reported type, or `InfoCarrierServerException` for unknown types.

Registered clients carry `infocarrier.client.name` and `infocarrier.client.registration`. The name
is empty for unnamed registrations. The number starts at one within each host and follows registration
order, distinguishing repeated unnamed registrations from a named client called `default`.
Directly constructed clients omit these labels. URLs, caller identities, and transaction tokens are
not metric labels. A host serving incoming requests also publishes separate
[server instruments](diagnostics.md#counting-server-requests).

Any OpenTelemetry exporter reads the same meter with `AddMeter("InfoCarrier.Core")`. Nothing is
measured while nothing is listening.

## Counting server requests

The `InfoCarrier.Core` meter publishes `infocarrier.server.requests` and
`infocarrier.server.request.duration` for requests that reach the InfoCarrier HTTP handler. They count completions and
record seconds through response writing, including streamed queries. Each carries
`infocarrier.operation`, `infocarrier.outcome`, and `error.type` on failure. Unreadable envelopes
and unrecognized operation values use `Unknown` as the operation.

`infocarrier.endpoint.name` is the configured server name, empty for unnamed registrations.
`infocarrier.endpoint.registration` starts at one within the host and follows server registration
order. Retained unnamed handles keep distinct numbers; mapping the same registration twice keeps
its number. Client and server numbering are independent. Low-level mappings without a registration
omit these labels. Route patterns, URLs, caller identities, and transaction tokens are not labels.
See [Counting round trips](diagnostics.md#counting-round-trips) for collector setup and outcome values.

## Inspect individual requests

A transport decorator can record operations and elapsed time.
The [Northwind sample](../getting-started/samples.md) also shows decoded payloads.
Do not record credentials, transaction tokens, or private values in production logs.
A response may contain server exception text and source paths.
See [custom transports](../extending/transports.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
