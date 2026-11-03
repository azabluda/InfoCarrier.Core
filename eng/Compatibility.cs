#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/Compatibility.cs

return await CompatibilityCommand.Run(args);
