#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/DocWords.cs

// Count prose using the documented budgets. Usage: [--all] [--budget] [file ...].
// --budget returns 1 when a page exceeds its limit; code and link destinations are excluded.
return DocWords.Run(args);
