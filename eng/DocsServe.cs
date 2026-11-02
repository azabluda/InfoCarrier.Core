#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/DocsServe.cs

// Preview the MkDocs site or build it strictly. Usage: [--build] [MkDocs serve arguments ...].
// The wrapper creates .venv when needed; MkDocs still needs its external interpreter.
return await ScriptHost.RunAsync(token => DocsServe.Run(args, token), "docs-serve");
