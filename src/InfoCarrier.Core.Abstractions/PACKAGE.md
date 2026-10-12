# InfoCarrier.Core.Abstractions

Shared deployment configuration contracts for InfoCarrier.Core applications.

Use this package from application code referenced by both the client and server. It contains only
`InfoCarrierDeploymentBuilder` and `IInfoCarrierValueMapper`. It has no Entity Framework Core
dependency.

An existing shared model project is usually the simplest home for deployment configuration. You
can instead use a separate shared deployment project when the model must not know about
InfoCarrier. A small application can also link one source file into both application projects.

```csharp
public static InfoCarrierDeploymentBuilder ConfigureInfoCarrier(
    this InfoCarrierDeploymentBuilder builder)
{
    builder.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
    return builder;
}
```

See the [InfoCarrier.Core documentation](https://infocarrier.dev/) for setup and examples.
