#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/SuiteSummary.cs

// Report independent TRX counters and parity; dotnet test remains the pass/fail gate.
// Usage: <results.trx> [more.trx ...]. Output files live beside the first TRX.
return await ScriptHost.RunAsync(token => SuiteSummary.Run(args, token), "suite-summary");
