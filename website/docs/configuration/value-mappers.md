# Value mappers

Most values cross the wire without you thinking about them: numbers, strings, dates, enums, GUIDs,
byte arrays, and any type the model declares a value converter for. A few do not, and a value mapper
is the seam for those.

## When you need one

The wire's default handling of a non-primitive value is to walk its public readable members. That is
right for an anonymous type, a record or a DTO — and wrong for a type whose members are *computed*:

- `NetTopologySuite.Geometries.Geometry` exposes `Boundary` and `Envelope`, both of which return
  geometries. Walking one recurses until the stack overflows.
- `System.Net.IPAddress.ScopeId` throws `SocketException` for an IPv4 address.
- `System.Uri.AbsolutePath` throws for a relative URI.

A mapper claims such a value and writes it as **one** wire primitive instead.

## What ships

`IPAddress` and `Uri`. Both are BCL types whose members throw for perfectly ordinary instances, so an
application storing one has opted into nothing and should not have to discover this seam.

They are registered for you on the client. **On the server you register them yourself**, because a
server builds its own service collection:

```csharp
builder.Services.AddInfoCarrierStandardValueMappers();
```

A geometry mapper is deliberately *not* in the box: shipping one would put a NetTopologySuite
dependency in the package for a type most callers never use. Spatial types are fully supported —
you supply the mapper, which is about thirty lines. There is a worked one in the repository's test
utilities.

## Writing one

Two methods, both of which may decline. A value no mapper claims falls through to exactly the
behaviour it has today, so registering one cannot change how anything else travels.

```csharp
using System.Globalization;
using System.Text.Json;
using InfoCarrier.Core.ValueMapping;

public readonly record struct Money(decimal Amount, string Currency);

public sealed class MoneyValueMapper : IInfoCarrierValueMapper
{
    public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
    {
        if (value is not Money money)
        {
            wireValue = null;
            return false;                                       // (1)
        }

        wireValue = $"{money.Amount.ToString(CultureInfo.InvariantCulture)} {money.Currency}";
        return true;
    }

    public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
    {
        value = null;

        if (declaredType != typeof(Money))
        {
            return false;
        }

        string text = wireValue switch                          // (2)
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString()!,
            _ => throw new InvalidOperationException($"Money arrived as '{wireValue?.GetType().Name}'."),
        };

        string[] parts = text.Split(' ');
        value = new Money(decimal.Parse(parts[0], CultureInfo.InvariantCulture), parts[1]);
        return true;
    }
}
```

1. Declining is how a mapper says "not mine". A mapper that returns `true` **must** produce a
   non-null wire value; claiming a value and producing nothing is an error.
2. **After a serialization round trip a wire primitive arrives as a `JsonElement`**, not as the CLR
   type that was written. Convert it rather than casting it — this is the mistake to expect.

The wire value must be one of the primitives the serializer knows: in practice a `string` or a
`byte[]`.

### Match on the CLR type alone

Decide from `value` and `declaredType`, never from a type mapping. The client's model is built by
this provider and the server's by your real provider, so a type mapping has *two* answers and they
need not agree. The CLR type has one.

## Registering it

The mapper must be registered on **both halves**, or a value that crosses in both directions is
mapped in one direction only.

=== "Server"

    ```csharp
    builder.Services
        .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
        .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
        .AddInfoCarrierStandardValueMappers()
        .AddSingleton<IInfoCarrierValueMapper, MoneyValueMapper>();
    ```

=== "Client"

    ```csharp
    ServiceProvider providerServices = new ServiceCollection()
        .AddEntityFrameworkInfoCarrier()
        .AddSingleton<IInfoCarrierValueMapper, MoneyValueMapper>()
        .BuildServiceProvider();

    DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
        .UseInternalServiceProvider(providerServices)
        .UseInfoCarrier(client)
        .Options;
    ```

    Build that service provider **once** and share it across contexts.

Your own mappers get first refusal, ahead of anything the library derives from the model, so
registering one is never overridden.

## A converter in the model is usually enough

If the model already declares a value converter for the type, the wire uses it and you need no
mapper:

```csharp
modelBuilder.Entity<Customer>()
    .Property(c => c.Balance)
    .HasConversion(
        m => $"{m.Amount} {m.Currency}",
        s => Money.Parse(s));
```

Both halves build the model from the same source, so both derive the same conversion. Reach for a
mapper when there is no converter — or when the CLR type is dangerous to walk regardless of how it
is stored.
