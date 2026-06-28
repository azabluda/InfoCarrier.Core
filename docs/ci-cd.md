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

### LocalDB for SqlServer Tests

Windows runners have LocalDB pre-installed. For Linux runners, SqlServer tests are skipped
(they need Windows).

## Pre-commit Hooks

Recommended but not mandatory:
- `dotnet format` (code style)
- `dotnet test --filter "FullyQualifiedName~InMemory"` (fast sanity check)
