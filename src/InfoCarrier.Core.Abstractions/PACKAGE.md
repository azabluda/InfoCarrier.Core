# InfoCarrier.Core.Abstractions

Shared deployment configuration contracts for InfoCarrier.Core applications.

Use this package from application code referenced by both the client and server. It contains only
`InfoCarrierDeploymentBuilder`, its shared fluent extensions, and `IInfoCarrierValueMapper`. It has no Entity Framework Core
dependency.

An existing shared model project is usually the simplest home for deployment configuration. You
can instead use a separate shared deployment project when the model must not know about
InfoCarrier. A small application can also link one source file into both application projects.

```csharp
public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
}
```

Apply the method through `ConfigureShared` in each role callback. It executes immediately on the
existing builder and preserves the surrounding client or server chain. Shared methods can compose
other ordinary methods. Existing ordering rules apply; later matching mappers do not override
earlier ones. Sharing a function does not prove agreement between separately deployed applications.

See the [InfoCarrier.Core documentation](https://infocarrier.dev/) for setup and examples.
