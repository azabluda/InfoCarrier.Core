# Operate transactions across requests

A transaction keeps a database context and connection on one server process.
Keep it short. Do not leave a transaction open while a user edits a form.

## Route requests to the same process

Every request carrying the transaction token must reach the server process that created it.
Configure session affinity in the load balancer. A restart loses that process's open transactions.
A token is not a way to transfer a transaction to another process.

An ordinary request gets a fresh context. A transaction keeps its context until it ends.
The context that begins the transaction owns it.
A [joining client context](../applications/transactions.md#two-contexts-one-transaction) can detach
without committing or rolling back the owner's transaction.

## Evicting an abandoned transaction

A client that vanishes mid-transaction leaves the server holding a context and a connection; its
own `DisposeAsync` cannot help, because the process is gone. Tell the server how long to wait.

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>(options =>
    options.WithTransactionIdleTimeout(TimeSpan.FromMinutes(10)));
```

Idle expiry is disabled by default. Every request naming the token refreshes the clock.
The setting limits idle time, not total transaction duration.

An eviction rolls the transaction back and logs at `Warning`. **A later commit then fails rather
than reporting success**, because the work is gone. A rollback stays silent, so disposing after a
commit still behaves.

## Binding a transaction to its caller

The token that names a transaction is a bearer credential: whoever holds it can query and save
inside that transaction, not merely end it. Tell the server who a caller is, and it refuses a token
opened by somebody else.

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>()
    .AddHttpCallerIdentity(http => http.User.FindFirst("sub")?.Value);
```

Caller binding is disabled by default. Authenticate the endpoint with `RequireAuthorization`
before relying on it. Anonymous callers with the same identity value are not distinguished.

**Choose a value that stays the same for as long as a transaction lives**, because a caller whose
value changes mid-transaction loses its own work. You pick it: a subject id, a login name and a
tenant claim behave differently under a token refresh.

Caller binding needs no extra identity field in the client message.

The caller rule belongs to this server registration. A host with several endpoints can give each
endpoint a different rule. The HTTP context accessor remains shared host infrastructure.

## Recover from a lost response

A failed commit response does not establish whether the database committed.
Do not call commit repeatedly or retry into an uncertain transaction.
Use a durable marker and a fresh verification context as described in
[write recovery](write-recovery.md).

For ordinary transaction and savepoint code, see [application transactions](../applications/transactions.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
