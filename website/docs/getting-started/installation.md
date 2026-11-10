# Install the packages

Use NuGet to add InfoCarrier to your application.
Install the .NET 10 SDK to follow the [first tutorial](first-app.md).

This is the draft installation guide for version 11, whose packages are not published yet.
The commands below describe installation for that release.
For available version 10.3 packages, use the [published guide](https://azabluda.github.io/InfoCarrier.Core/getting-started/installation/).

## Client

In the client project:

```sh
dotnet add package InfoCarrier.Core
```

The client uses your shared context model with InfoCarrier as its database provider.
It does not need a database driver or connection string.

## Server

In the ASP.NET Core server project:

```sh
dotnet add package InfoCarrier.Core.AspNetCore
```

This includes Core as a dependency.
Add the Entity Framework Core provider for the server's database too.
The tutorial uses SQLite.

Use matching InfoCarrier versions on client and server.
The current version 11 draft still targets Entity Framework Core 10; its adoption status is recorded
in the [11.0 notes](../release-notes/11.0.md).

## Shared model

Keep your entity classes and context model in an application project referenced by both sides.
That project needs Entity Framework Core. It does not need InfoCarrier merely to declare the model.

`InfoCarrier.Core.Abstractions` is optional for a separate project containing additional shared
InfoCarrier settings. Basic queries and saves do not need that extra project or registration.

## Try a complete application

[Build a client and server](first-app.md) from an empty directory.
It shows every package command, the shared model, one server endpoint, and a client query followed by a save.

For an existing application, see the [upgrade guide](../upgrades/index.md).


## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
