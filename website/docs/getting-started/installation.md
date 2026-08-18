# Installation

## Two packages

| Package | Reference it from | What it costs |
|---|---|---|
| `InfoCarrier.Core` | your **client**, and your **server** | one dependency: `Microsoft.EntityFrameworkCore` |
| `InfoCarrier.Core.AspNetCore` | your **server** only | a framework reference to `Microsoft.AspNetCore.App` |

The split is on the only line that costs anything. HTTP support lives in `InfoCarrier.Core` because
`System.Net.Http` is in the shared framework and is therefore safe in Blazor WebAssembly. The
ASP.NET Core endpoint is a second package precisely because it is *not* free: a WPF, MAUI or
WebAssembly client must not have to be an ASP.NET Core app to restore its data-access library.

A typical solution has three projects:

```text
Shop.Shared     ── the entity classes and the DbContext         → InfoCarrier.Core
Shop.Client     ── WPF / Blazor / MAUI / console                → Shop.Shared
Shop.Server     ── ASP.NET Core + a real EF Core provider       → Shop.Shared,
                                                                  InfoCarrier.Core.AspNetCore
```

The `DbContext` and the entity classes are **shared source**. Both halves build the same model from
the same code, which is what makes the wire format meaningful — see
[Your first client and server](first-app.md).

## Installing

=== "dotnet CLI"

    ```bash
    # In the client project — and in the server project too.
    dotnet add package InfoCarrier.Core --version 10.0.0-preview.1

    # In the server project only.
    dotnet add package InfoCarrier.Core.AspNetCore --version 10.0.0-preview.1
    ```

=== "PackageReference"

    ```xml
    <ItemGroup>
      <PackageReference Include="InfoCarrier.Core" Version="10.0.0-preview.1" />
      <PackageReference Include="InfoCarrier.Core.AspNetCore" Version="10.0.0-preview.1" />
    </ItemGroup>
    ```

=== "Package Manager"

    ```powershell
    Install-Package InfoCarrier.Core -Version 10.0.0-preview.1
    Install-Package InfoCarrier.Core.AspNetCore -Version 10.0.0-preview.1
    ```

!!! danger "Always name the version — an unversioned install gets the *previous generation*"

    `InfoCarrier.Core` has been on nuget.org since v1, and its newest **stable** release is
    `3.1.1`, built for **EF Core 3.1**. NuGet prefers a stable release over a prerelease, so:

    ```bash
    dotnet add package InfoCarrier.Core              # installs 3.1.1 — NOT this library
    dotnet add package InfoCarrier.Core --version 10.0.0-preview.1   # correct
    dotnet add package InfoCarrier.Core --prerelease                 # also correct
    ```

    It does not fail, and it does not warn. You get a different, incompatible library that targets
    an EF Core seven majors old. This stays true until a stable `10.x` ships.

    `InfoCarrier.Core.AspNetCore` is new in this generation and has no older version to fall back
    to — but pin it anyway, so the two halves cannot drift apart.

    The same applies to Central Package Management: pin `10.0.0-preview.1` in
    `Directory.Packages.props`.

Both packages ship symbol packages and SourceLink, so you can step into the provider from a
debugger with no extra configuration.

### Building from source instead

Nothing beyond the .NET 10 SDK is needed:

```bash
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
dotnet pack InfoCarrier.Core.slnx -c Release -o artifacts/pack
```

Both packages land in `artifacts/pack`; every other project in the solution opts out of packing.

## Requirements

| | |
|---|---|
| Runtime | .NET 10 |
| EF Core | 10.0 |
| Server-side provider | any — SQL Server, PostgreSQL, SQLite, InMemory … |
| Client platforms | anywhere .NET 10 runs, including Blazor WebAssembly |

The server's provider is entirely your choice: it is an ordinary EF Core application, and
InfoCarrier.Core never sees the connection. The one platform with constraints of its own is the
browser — see [Blazor WebAssembly](../platforms/blazor-webassembly.md).

## Versioning

The version tracks EF Core's major, as every EF Core provider does: `10.0.x` targets EF Core 10.
The `-preview` suffix stays until a gRPC binding and streaming results are settled, because both
may change the transport interface.
