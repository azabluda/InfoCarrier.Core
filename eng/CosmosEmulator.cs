#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include shared/ScriptHost.cs
#:include commands/CosmosEmulator.cs

// Usage: start|stop|logs podman|docker. The runtime must already be running.
return await ScriptHost.RunAsync(token => CosmosEmulatorCommand.Run(args, token), "cosmos-emulator");
