# InfoCarrier.Core.Abstractions

InfoCarrier.Core.Abstractions is a package for shared deployment configuration.
It has no Entity Framework Core dependency.
Reference it from shared application code that both client and server can use.

## Usage

For version 11 development code:

```csharp
using InfoCarrier.Core;

public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = "" });
}
```

Call this method through `ConfigureShared` in both client and server callbacks.
Use the same definitions on both sides.
Shared source does not verify that separately deployed applications use matching settings.

## Getting started

See [shared configuration](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/applications/configuration/shared.md).
Core already depends on this package. Add a direct reference when a shared project needs only
the configuration builder and value-mapper interface.

## Additional documentation

Use the [development installation guide](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/getting-started/installation.md)
for matching package versions.
The [public documentation](https://azabluda.github.io/InfoCarrier.Core/) describes the published release.

## Feedback

[Open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new) for questions or problems.
