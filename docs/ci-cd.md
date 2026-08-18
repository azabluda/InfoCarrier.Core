# CI/CD Strategy

## GitHub Actions

### `build.yml` — Build & Test

Trigger: `push` to `main`/`develop`, `pull_request` to `main`.

Runs on `ubuntu-latest` only — no Windows matrix needed. SQL Server is provided
via a Docker service container, which works identically on Ubuntu.

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
    runs-on: ubuntu-latest
    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          SA_PASSWORD: InfoCarrier1!
        ports:
          - 1433:1433
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore InfoCarrier.Core.sln
      - run: dotnet build InfoCarrier.Core.sln --no-restore -c Release
      - run: dotnet test test/InfoCarrier.Core.FunctionalTests/ --no-build -c Release --filter "FullyQualifiedName~InMemory"
      - run: dotnet test test/InfoCarrier.Core.FunctionalTests/ --no-build -c Release --filter "FullyQualifiedName~SqlServer"
        env:
          INFOCARRIER_SQL_CONNECTION: Server=localhost,1433;Database=InfoCarrierTest;User=sa;Password=InfoCarrier1!;TrustServerCertificate=true
```

### `release.yml` — pack, gate, attach

Trigger: tag push (`v*`), or `workflow_dispatch` to rehearse without a tag.

- Runs the same gates `build.yml` does, then `dotnet pack InfoCarrier.Core.slnx -c Release`
  with `ContinuousIntegrationBuild=true`
- Checks the tag against the packaged version, and fails if they disagree
- Attaches the `.nupkg` and `.snupkg` files to a GitHub Release

Then a second job, `publish-nuget`, **waits for a human**. It runs in the `nuget-org`
environment, which has required reviewers, so nothing reaches nuget.org until somebody approves
it. On approval it pushes `InfoCarrier.Core`, then `InfoCarrier.Core.AspNetCore`, then the symbol
packages — in that order, because the second declares a dependency on the first and nuget.org
rejects a package whose dependency does not resolve.

**There is no publishing secret in this repository.** The job exchanges a GitHub OIDC token for a
nuget.org API key valid for one hour (Trusted Publishing), so nothing long-lived is stored. It
needs `id-token: write`, and a policy on nuget.org naming this owner, repository, workflow file and
environment. See [`versioning.md`](versioning.md).

### `packages.yml` — the internal feed

Trigger: every push to `main` that touches something other than documentation.

Packs and pushes to GitHub Packages with the run's own `GITHUB_TOKEN`; there is no secret to
configure. MinVer gives each commit a unique ordered prerelease, so a build of any branch is
installable by name. Consuming it needs a PAT with `read:packages`, which is a GitHub limitation
and the reason nuget.org stays the public route.

### Versioning

The git tag is the version — see [`versioning.md`](versioning.md). Every workflow that builds
checks out with `fetch-depth: 0`, because MinVer reads the tags and a shallow clone has none.

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
