# Prepare for the first application

The [first tutorial](first-app.md) uses the version 11 development source.
Install the .NET 10 SDK, version 10.0.300 or later, and Git.

This checkout currently uses Entity Framework Core 10.
For an application using published version 10.3 packages, use the
[public documentation](https://azabluda.github.io/InfoCarrier.Core/).

## Get the source

Run these commands in PowerShell:

```powershell
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
dotnet --version
```

Check that the SDK version meets the requirement above.
You do not need to build the whole repository before starting the tutorial.
Its project references build the libraries the example needs.

Continue with [your first client and server](first-app.md).
It creates the projects, adds references, installs the SQLite provider, and supplies all application code.

## Which projects use which package?

| Package | Purpose |
|---|---|
| `InfoCarrier.Core` | Client queries and change tracking |
| `InfoCarrier.Core.AspNetCore` | The server's HTTP endpoint; depends on Core |
| `InfoCarrier.Core.Abstractions` | Shared deployment settings without an Entity Framework Core dependency |

The first tutorial puts the context and entities in `Shop.Shared`, referenced by both applications.
Only the server installs a database provider.
Abstractions is already a dependency of Core. You do not need a separate configuration project
for the tutorial.

## Other ways to use development builds

These alternatives are for integrating development code into an existing application.
They are not required for the first tutorial.

### Local packages

From the repository root:

```powershell
dotnet pack InfoCarrier.Core.slnx -c Release -o artifacts/pack
```

All three packages are written to `artifacts/pack`.
Add that folder as a local NuGet source and select matching package versions on both sides.

### GitHub Packages

The Packages workflow publishes development packages to
`https://nuget.pkg.github.com/azabluda/index.json`.
Configure that feed using
[GitHub's NuGet authentication instructions](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).
Keep credentials outside source control.

Select a version actually present in the feed.
Adding `--prerelease` without configuring the feed does not install version 11 from nuget.org.

For upgrades, use the [upgrade index](../upgrades/index.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
