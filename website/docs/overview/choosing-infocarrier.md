# Choosing InfoCarrier

Use InfoCarrier when a .NET client needs to compose queries and track edits while an application
server controls access to the database. The client and server share a context model.

For example, an order screen can filter and page its own query, load an order's customer,
edit several values, and save them together. You do not need a separate HTTP action for every
query that screen might compose.

## What application code can do

| Task | How you do it |
|---|---|
| Find, filter, and page data | Ordinary LINQ with Entity Framework Core asynchronous methods |
| Edit data | Load entities, change properties, and call `SaveChangesAsync` |
| Load relationships | `Include` or explicit asynchronous loading |
| Group work across requests | Transactions and savepoints supported by the server provider |
| Change many rows without loading them | Supported `ExecuteUpdate` and `ExecuteDelete` operations |
| Read a large result progressively | Asynchronous non-tracking queries |

The server uses the database provider selected by your application.
Its translation rules still determine which queries it can run.
InfoCarrier transports the work; it does not add database features.

## What you share

Keep entity definitions and context configuration in shared application code.
The [first tutorial](../getting-started/first-app.md) shows a small three-project solution.

This arrangement suits applications whose client and server models can evolve together.
Plan compatible deployments when they update separately.
See [client/server compatibility](../reference/compatibility.md).

If your client must use a separately designed public data contract, consider whether exposing
query composition over the shared model meets that requirement.

## Plan for network requests

A query, a save, or a navigation load can require a server request.
Batch related edits into one save and page lists on the server.
A screen that loads relationships one by one can make many requests.
The [related-data guide](../applications/related-data.md) explains the choices.

The built-in client uses HTTP. The server owns the database connection.
Client code therefore cannot open a local database connection or run schema migrations.
Create and update the schema on the server.

## Plan for access control

The endpoint accepts queries and writes.
Authenticate callers and enforce their permissions on the server.
Client-side restrictions and server query filters alone do not provide authorization.
Use the [deployment security guide](../operations/security.md) before exposing an application.

A lost connection can leave a write's outcome unknown.
Applications that need recovery must check whether the operation committed before retrying.
See [write recovery](../operations/write-recovery.md).

## Check a specific requirement

Start with the normal application guides.
Use [detailed limitations](../reference/limitations.md) for unsupported operations and less common
behavior differences. For a browser application, follow the [Blazor guide](../applications/blazor-webassembly.md)
after the first tutorial.

To try the architecture, [build the SQLite example](../getting-started/first-app.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
