# Limitations

InfoCarrier.Core runs Microsoft's own Entity Framework Core specification suite, the same suite the
SQL Server, SQLite and InMemory providers run. This page lists every scenario in that suite which
does not behave the way a normal EF Core provider behaves, so you can judge whether any of them
affects your application.

It is complete for what a caller can observe as a limitation. The suite's other differences ask
the client for something only a database has, assert a refusal this provider does not need to make,
or are EF Core defects that every provider hits and this one reports with a different exception
type.

```
Total tests: 29958, Failed: 0
```

Measured against `10.2.0`. No test fails. Where this provider answers differently from EF Core, the
test that covers it says so, and this page names the differences you can observe. The skipped tests
are EF Core's own: EF skips them itself for the store behind them, and none is a suppression added
here.

## Not supported

### Suppressing a concurrency exception in an interceptor on the client

Affects you if an `ISaveChangesInterceptor` registered on the client returns
`InterceptionResult.Suppress()` from `ThrowingConcurrencyException` or
`ThrowingConcurrencyExceptionAsync`.

With EF Core, a suppression skips the conflicting row and the rest of the save goes ahead. Here the
server finds the conflict and rolls the save back before the client's interceptor runs.
`SaveChanges` returns 0 and does not throw. **Nothing in that save is written, and the change
tracker marks every change in it as saved.**

Register the interceptor on the server instead. The server runs EF Core's own save, so a
suppression there behaves as it does with EF Core: the other changes are written, and the client's
`SaveChanges` returns the count EF Core would.

```csharp
// On the server
builder.Services.AddDbContext<ShopContext>(o => o
    .UseSqlServer(connectionString)
    .AddInterceptors(new IgnoreConcurrencyConflicts()));
```

An interceptor on the client still sees the exception, so it can log it and let it throw.

## Differences that are not limitations

These behave correctly, and differ from another EF Core provider only in ways you would notice
when porting code or tests.

### Exception message text for an untranslatable query

When a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does. The message text may differ. Two examples:

```csharp
// (a) a method call where ExecuteUpdate expects a property
context.Orders
    .Where(o => o.Total > 100m)
    .ExecuteUpdate(s => s.SetProperty(o => Math.Round(o.Total), 0m));

// (b) a cast to a type no mapped entity implements
IQueryable orders = context.Orders;
orders.Cast<IArchivable>().FirstOrDefault();
```

Both throw. Catch the exception type and do not match on message text, which is unsupported on any
EF Core provider.

### Queries this provider answers that other providers do not

One scenario in EF's suite expects the provider to reject the query, and this provider answers it.
Composing LINQ over a collection stored through a value converter:

```csharp
modelBuilder.Entity<Dashboard>()
    .Property(e => e.Layouts)
    .HasConversion(                       // List<Layout> stored as a single string column
        v => Serialize(v),
        v => Deserialize(v),
        layoutComparer);

context.Dashboards
    .Select(d => new { d.Name, Heights = d.Layouts.Select(l => l.Height).ToList() })
    .ToList();
// EF Core providers: throws.   This provider: reads the column and answers.
```

The inner lambda names `Layout`, which the model does not imply, so this client keeps that part of
the query and the server sends the whole column. Name `Layout` on both halves, as
[Keys of your own types](guide/querying.md#keys-of-your-own-types) describes for a `GroupBy` key,
and the query reaches the server, which rejects it as every other provider does.

A test suite you port will expect the exception, and LINQ that relies on the answer will not run
unchanged elsewhere.

## Consequences of the client having no database

These are not defects. They follow from where the client sits.

| | |
|---|---|
| Relational-only APIs, such as `ExecuteSqlRaw`, `GetDbTransaction` and migrations, are not part of this provider's surface. Calling one throws. `FromSql` runs only where the server has granted it, and that grant is arbitrary SQL | [Querying](guide/querying.md#what-is-not-part-of-the-surface) |
| Automatic lazy loading does not work in Blazor WebAssembly | [Blazor WebAssembly](platforms/blazor-webassembly.md) |
| The client never sees the server's provider, so it assumes a relational store and refuses three queries relational providers refuse. `UseInfoCarrier(client, o => o.UseNonRelationalServerStore())` says otherwise | [Querying](guide/querying.md#rules-that-come-from-the-servers-store) |
| The server's provider writes the SQL, so how a query is translated is settled there, not on the client. `EF.Constant` and `EF.Parameter` are part of your query, so they cross the wire and the server honours them | |
| `EF.CompileQuery` changes nothing about the wire. The server answers a compiled query once per execution, as it answers any other query | [Querying](guide/querying.md#round-trips-and-result-size) |
| A query result arrives in one response rather than as a stream, so a very large result set is a very large response. Page it. | |
| Authentication and authorization are yours | [Security](security.md) |
| Native AOT is not supported: remoting a query means compiling an expression tree at runtime. Trimming is a separate question, and it works. | [Blazor WebAssembly](platforms/blazor-webassembly.md#trimming) |

## What this page cannot tell you

Every entry above corresponds to tests in EF Core's specification suite that run on every build.
A failing test breaks the build, so a new entry cannot appear unnoticed. When an entry is fixed,
or a new one appears, this page changes with it.

What the suite measures bounds what this page can promise. A conformance suite says nothing about
performance, payload size or concurrency under load. A scenario it never exercises is outside what
this page claims at the top.
