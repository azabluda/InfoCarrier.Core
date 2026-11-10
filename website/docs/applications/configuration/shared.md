# Share model and deployment settings

Both applications must agree on the entity types and properties used in requests.
Start with the same [context and entity classes](../../getting-started/first-app.md).
Enable model-changing options on both sides unless the platform guide documents an exception.

Put additional settings in an ordinary shared method:

```csharp
using InfoCarrier.Core;

public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = "" });
}
```

Use it in both applications:

```csharp
// Client application
builder.Services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

// Server application
builder.Services.AddInfoCarrierServer<ShopContext>(o =>
    o.ConfigureShared(ShopDeployment.ConfigureInfoCarrier));
```

A separate shared configuration project can reference `InfoCarrier.Core.Abstractions`.
That package has no Entity Framework Core dependency.
You can also keep the method in the shared model project or link the same source file into both applications.

## What belongs in the method

| Setting | Purpose |
|---|---|
| `AddAllowedTypes(typeof(...))` | Permit named types beyond those known from the model |
| `AddAnonymousShapes(new { ... })` | Describe anonymous object structures that the server must understand |
| `AddValueMappers(new ...)` | Convert custom values in both directions |
| `AllowArbitrarySqlExecution(true)` | Permit raw SQL; the server must grant it as well |

Keep addresses, byte limits, timeouts, and log forwarding in the separate client/server callbacks.

`ConfigureShared` calls your method immediately, once, at that point in the chain.
It returns the same builder, so client/server methods can follow it.
Shared methods can call other shared methods or receive settings through a lambda.
The first matching mapper wins; adding another matching mapper later does not replace it.

Using shared source helps prevent mistakes. It does not check that separately deployed applications
use the same build or configuration.

## Register types without promising translation

A named key or helper type must exist in both applications.
Registering it allows the expression to reach the server.
The database provider then decides whether it can translate that expression.
InfoCarrier does not retry a server refusal by downloading rows and running the query locally.

Register trusted types only. This permission can expose their public methods.
See [security](../../operations/security.md), [querying](../querying.md), and
[value mappers](../../extending/value-mappers.md).

## Register anonymous structures

Prototype values are ignored. Property names, order, and types define the structure.
Both applications can declare it in different assemblies.

```csharp
builder.AddAnonymousShapes(
    new { Country = "", City = "" },
    new { CustomerId = "", Year = 0 });
```

These registrations can cover grouping keys, join keys, and nested data as well as projections.
Named records use `AddAllowedTypes` instead.
Nested anonymous objects are registered recursively, including those inside arrays and generic collections.

An unknown structure used by server execution is refused.
Supported final result construction can still take place on the client.
Registering a structure does not grant permission to custom types inside it.

Different anonymous types with the same registered structure cannot share one exchange.
They are refused to avoid changing equality behavior or restoring the wrong client type.
Keep prototypes in trusted startup code. See [reference limits](../../reference/options.md#anonymous-structures).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
