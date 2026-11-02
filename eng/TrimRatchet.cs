#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/TrimRatchet.cs

// Clean-publish the Blazor client and enforce the existing InfoCarrier trim-warning limit.
// Usage: [baseline-file]. Cleanup, publish, and missing-trimmer errors fail the check.
return await ScriptHost.RunAsync(token => TrimRatchet.Run(args, token), "trim-ratchet");
