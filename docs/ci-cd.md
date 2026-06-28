# CI/CD Strategy

## GitHub Actions

### `build.yml` — Build & Test

Trigger: `push` to `main`/`develop`, `pull_request` to `main`.

```yaml
name: Build & Test
on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '10.0.x'

jobs:
  build-and-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]     # SqlServer tests need Windows
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore InfoCarrier.Core-v2.sln
      - run: dotnet build InfoCarrier.Core-v2.sln --no-restore -c Release
      - run: dotnet test test/InfoCarrier.Core.FunctionalTests/ --no-build -c Release --filter "FullyQualifiedName~InMemory"
      - run: dotnet test test/InfoCarrier.Core.FunctionalTests/ --no-build -c Release --filter "FullyQualifiedName~SqlServer"
        if: matrix.os == 'windows-latest'
```

### `release.yml` — NuGet Pack & Publish

Trigger: tag push (`v*`).

- `dotnet pack src/InfoCarrier.Core/InfoCarrier.Core.csproj -c Release`
- `dotnet nuget push` to NuGet.org (needs `NUGET_API_KEY` secret)

### Docker SQL Server for SqlServer Tests

Both local development and CI use a Docker SQL Server container — NOT LocalDB.
This ensures identical behavior across dev machines and CI runners (Ubuntu + Windows).

**Local dev**: `docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=InfoCarrier1!' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`

**CI (GitHub Actions)**: Use a service container:
```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      ACCEPT_EULA: Y
      SA_PASSWORD: InfoCarrier1!
    ports:
      - 1433:1433
```

**Test fixture**: The `InfoCarrierTestStoreFactory.SqlServer` should launch a pristine
SQL Server container (or reuse an existing one) and create a fresh database per test batch.
Connection string: `Server=localhost,1433;Database=InfoCarrierTest_{guid};User=sa;Password=InfoCarrier1!;TrustServerCertificate=true`

This approach works identically on Ubuntu and Windows runners, and on developer machines
with Docker installed.

## Pre-commit Hooks

Recommended but not mandatory:
- `dotnet format` (code style)
- `dotnet test --filter "FullyQualifiedName~InMemory"` (fast sanity check)
