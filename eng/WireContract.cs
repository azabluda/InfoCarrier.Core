#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/WireContract.cs

// Compare the candidate with protected Git descriptions. --record writes only after every comparison passes.
// Usage: [--base-ref revision] [--record] [--release-ref revision ...]. See docs/wire-protocol.md.
return await WireContractCli.Run(args);
