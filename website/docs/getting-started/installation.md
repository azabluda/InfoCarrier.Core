# Installation

## Two packages

| Package | Reference it from |
|---|---|
| `InfoCarrier.Core` | your client, and your server |
| `InfoCarrier.Core.AspNetCore` | your server only |

A client references `InfoCarrier.Core` alone. It adds one dependency,
`Microsoft.EntityFrameworkCore`, and the HTTP transport is inside it. The server endpoint is a
second package because it carries a framework reference to `Microsoft.AspNetCore.App`, which a WPF,
MAUI or WebAssembly client should not have to satisfy to restore a data-access library.

A typical solution has three projects:

```text
Shop.Shared     the entity classes and the DbContext        → InfoCarrier.Core
Shop.Client     WPF / Blazor / MAUI / console               → Shop.Shared
Shop.Server     ASP.NET Core and a real EF Core provider    → Shop.Shared,
                                                              InfoCarrier.Core.AspNetCore
```

The `DbContext` and the entity classes are shared source. Both halves build the same model from the
same code, which is what makes the wire format meaningful. See
[Your first client and server](first-app.md).

## Installing

=== "dotnet CLI"

    ```bash
    # In the client project, and in the server project too.
    dotnet add package InfoCarrier.Core

    # In the server project only.
    dotnet add package InfoCarrier.Core.AspNetCore
    ```

=== "PackageReference"

    ```xml
    <ItemGroup>
      <PackageReference Include="InfoCarrier.Core" Version="10.0.0" />
      <PackageReference Include="InfoCarrier.Core.AspNetCore" Version="10.0.0" />
    </ItemGroup>
    ```

=== "Package Manager"

    ```powershell
    Install-Package InfoCarrier.Core
    Install-Package InfoCarrier.Core.AspNetCore
    ```

With Central Package Management, pin the version in `Directory.Packages.props` and give both
packages the same one.

If you are moving an application off `3.1.1`, read [Upgrading from 3.1](upgrading-from-3-1.md).
Your model carries over; the wiring around it does not.

Both packages ship symbol packages and SourceLink, so you can step into the provider from a debugger
with no extra configuration.

### Building from source instead

Nothing beyond the .NET 10 SDK is needed:

```bash
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
dotnet pack InfoCarrier.Core.slnx -c Release -o artifacts/pack
```

Both packages land in `artifacts/pack`. Every other project in the solution opts out of packing.

## Requirements

| | |
|---|---|
| Runtime | .NET 10 |
| EF Core | 10.0 |
| Server-side provider | any: SQL Server, PostgreSQL, SQLite, InMemory … |
| Client platforms | anywhere .NET 10 runs, including Blazor WebAssembly |

The server's provider is your choice. The server is an ordinary EF Core application and
InfoCarrier.Core never sees the connection. The one platform with constraints of its own is the
browser: see [Blazor WebAssembly](../platforms/blazor-webassembly.md).

## Versioning

The version tracks EF Core's major, as every EF Core provider does. `10.0.x` targets EF Core 10.
