# Value mappers

Most values cross the wire without you thinking about them: numbers, strings, dates, enums, GUIDs,
byte arrays, and any type the model declares a value converter for. A few do not, and a value mapper
is the seam for those.

## Choose a converter or mapper

Most application values already work: numbers, strings, dates, enums, GUIDs, and byte arrays.
Built-in mappers handle `IPAddress` and `Uri`.

If a value has a model conversion, use that on both sides.
For example, add this Money type to the shared project:

```csharp
public readonly record struct Money(decimal Amount, string Currency);
```

Add the balance property inside Customer:

```csharp
public Money Balance { get; set; }
```

In `OnModelCreating`, configure its stored representation.
This example uses `System.Globalization` and the shared Money type:

```csharp
modelBuilder.Entity<Customer>()
    .Property(c => c.Balance)
    .HasConversion(
        m => m.Amount.ToString(CultureInfo.InvariantCulture) + " " + m.Currency,
        s => new Money(decimal.Parse(s.Substring(0, s.IndexOf(' ')), CultureInfo.InvariantCulture),
            s.Substring(s.IndexOf(' ') + 1)));
```

A model converter is usually enough for transport too.
Use a value mapper when no converter is available or the value needs special handling during exchange.

## When you need one

A mapper is useful when inspecting a value's public properties is not a safe representation.
Examples include:

- `NetTopologySuite.Geometries.Geometry` exposes `Boundary` and `Envelope`, both of which return
  geometries. Walking one recurses until the stack overflows.
- `System.Net.IPAddress.ScopeId` throws `SocketException` for an IPv4 address.
- `System.Uri.AbsolutePath` throws for a relative URI.

A mapper claims such a value and writes it as one wire primitive instead.

## Existing and spatial mappers

`IPAddress` and `Uri`. Both are standard .NET types whose members throw for perfectly ordinary instances. An
application that stores one has chosen nothing unusual, so it should not have to discover this seam
at all.

They are registered automatically on both roles:

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>();
```

For NetTopologySuite geometry values, supply a mapper appropriate to the representation both
applications use. The repository's test utilities contain a
[worked mapper](https://github.com/azabluda/InfoCarrier.Core/blob/main/test/InfoCarrier.Core.TestUtilities/InfoCarrierNetTopologySuiteValueMapper.cs).
Copy and adapt the mapper into your shared application project.

Register any extra helper types needed by spatial queries on both sides.
The server's provider decides translation. InfoCarrier does not silently download all rows to run
a refused grouping or aggregate locally. Choose local execution explicitly when that is intended.

## Write a mapper

This is an alternative to the model converter above. The example includes the Money declaration;
omit it if you already added that type to the shared project.

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

1. Declining is how a mapper says "not mine". A mapper that returns `true` must produce a non-null
   wire value; claiming a value and producing nothing is an error.
2. After a serialization round trip a wire primitive arrives as a `JsonElement`, not as the CLR type
   that was written. Convert it rather than casting it. This is the mistake to expect.

The wire value must be one of the primitives the serializer knows: in practice a `string` or a
`byte[]`.

### Match on the CLR type alone

Decide from `value` and `declaredType`, never from a type mapping. The client's model is built by
this provider and the server's by your real provider, so a type mapping has two answers and they
need not agree. The CLR type has one.

## Registering it

Register the mapper on both halves, or a value that crosses in both directions is mapped in one
direction only. Put the mapper in the same application-owned deployment method:

```csharp
// Client application
builder.Services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

// Server application
builder.Services.AddInfoCarrierServer<ShopContext>(o => o.ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder.AddValueMappers(new MoneyValueMapper());
}
```

Custom clients can apply the same callback through `InfoCarrierClient.Configure`.

Your own mappers get first refusal, ahead of anything the library derives from the model, so
registering one is never overridden.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
