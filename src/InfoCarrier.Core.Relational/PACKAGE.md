`InfoCarrier.Core.Relational` is the relational half of
[InfoCarrier.Core](https://www.nuget.org/packages/InfoCarrier.Core). Reference it on your client
and on your server when the server's database is a relational one.

The core package works against any Entity Framework Core provider, so it does not know what a SQL
query root looks like. This package tells it, and that is what lets `FromSql` and
`Database.SqlQuery<T>` cross the wire.

Both halves need .NET 10 and EF Core 10. Install with
`dotnet add package InfoCarrier.Core.Relational`, and give every InfoCarrier package the same
version.

## Usage

On the server, beside the services `InfoCarrier.Core` already needs:

```csharp
builder.Services.AddInfoCarrierRelational();
```

On a client that builds a service collection:

```csharp
builder.Services.AddInfoCarrierRelationalClient();
```

Use the client call on the client only. It also replaces the database facade, which is what
`Database.SqlQuery<T>` reads, and your server has Entity Framework Core's own facade over a live
connection.

A client with no service collection says the same thing on its options:

```csharp
optionsBuilder.UseInfoCarrier(
    client,
    o => o.UseRelationalQueryRoots(new InfoCarrierRelationalQueryRoots()));
```

## Raw SQL still needs a grant

This package says what a raw-SQL query is. It does not permit one. A query carrying SQL crosses
only when the client calls `AllowArbitrarySqlExecution()` and the server calls
`AddInfoCarrierArbitrarySqlExecution()`. A client cannot grant it alone, because the server checks
for itself.

## Additional documentation

See [Security](https://azabluda.github.io/InfoCarrier.Core/security/) for what a server will and
will not execute.

## Feedback

If you encounter a bug, have a question, or would like to request a feature,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
