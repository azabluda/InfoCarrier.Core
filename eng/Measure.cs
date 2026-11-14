#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/Measure.cs

// Run active specification projects and compare complete failure-name/reason snapshots.
// Usage: <label> [baseline-label]. Logs and snapshots live under artifacts/measure.
return await ScriptHost.RunAsync(token => Measure.Run(args, token), "measure");
