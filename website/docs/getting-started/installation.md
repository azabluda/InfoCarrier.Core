# Install packages

These pages describe the version 11 development branch. It currently uses .NET 10 and
Entity Framework Core 10. Version 11 is not yet a published stable package.
For published version 10.3, use the [public documentation](https://azabluda.github.io/InfoCarrier.Core/).

## Choose the packages

| Package | Where it belongs |
|---|---|
| `InfoCarrier.Core` | The client; also available to the server through the endpoint package |
| `InfoCarrier.Core.AspNetCore` | An ASP.NET Core server exposing the HTTP endpoint |
| `InfoCarrier.Core.Abstractions` | A shared configuration project that should not depend on Entity Framework Core |

Core depends on Abstractions and Entity Framework Core's core and relational libraries.
The relational library lets the client understand model information; it does not add a database driver.
AspNetCore depends on Core and the ASP.NET Core shared framework.
Only the server needs a database provider such as SQLite.

Share entity classes and the context model between both applications.
Put settings that must agree on both sides in a [shared method](../applications/configuration/shared.md).

## Use the development source

Install the .NET SDK required by `global.json`, then clone the repository:

```powershell
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
dotnet build InfoCarrier.Core.slnx
```

The [first application](first-app.md) uses project references to this checkout.
This avoids accidentally installing stable 10.3 packages and then calling version 11 methods.

To build local packages instead:

```powershell
dotnet pack InfoCarrier.Core.slnx -c Release -o artifacts/pack
```

All three packages are written to `artifacts/pack`. Select matching versions from that folder.
Add it as a local NuGet source in your application. Keep the same InfoCarrier version on both sides.

## Use development packages from GitHub

The repository's Packages workflow publishes development packages to
`https://nuget.pkg.github.com/azabluda/index.json`.
GitHub Packages requires authentication. Follow
[GitHub's NuGet authentication instructions](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
and keep credentials outside source control.

Select a version that exists in that feed and use the same version for Core, AspNetCore,
and any direct Abstractions reference. Adding `--prerelease` without configuring this feed
does not install version 11 from nuget.org.

## Requirements

| Item | Development requirement |
|---|---|
| Runtime | .NET 10 |
| Entity Framework Core | At least 10.0.1, below 11.0 |
| Server database | A provider supported by the server application; test the queries your application uses |
| Client | A .NET 10 application; browsers have [additional restrictions](blazor-webassembly.md) |

Entity Framework Core 11 adoption is pending. The
[11.0 draft notes](../release-notes/11.0.md) reserve a section for verified support.

For an existing application, start with [upgrade instructions](../upgrades/index.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
