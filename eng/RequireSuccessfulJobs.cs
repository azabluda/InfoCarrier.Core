#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/RequireSuccessfulJobs.cs

// Preserve one required verdict after independent GitHub Actions jobs finish.
// Usage: <job=result> [job=result ...]. Only success is accepted.
return RequireSuccessfulJobs.Run(args, Console.Error);
