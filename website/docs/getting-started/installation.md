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

!!! tip "Name the version, or pass `--prerelease`"

    ```bash
    dotnet add package InfoCarrier.Core --prerelease
    ```

    Without one or the other, NuGet looks for a stable release and there is not one yet. The same
    applies to Central Package Management: pin `10.0.0-preview.1` in `Directory.Packages.props`.

Both packages ship symbol packages and SourceLink, so you can step into the provider from a
debugger with no extra configuration.

### Building from source instead

Nothing beyond the .NET 10 SDK is needed. **Note the branch** — this is a ground-up rewrite for EF
Core 10, and the repository's default branch is still the previous major version:

```bash
git clone -b v10-claude https://github.com/azabluda/InfoCarrier.Core.git
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
