#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/SpecParity.cs

// Compute the specification parity badge. Usage: <reasons.tsv ...> -- <results.trx ...>.
// Values go to stdout; the Markdown table goes to stderr. Empty or invalid input fails.
return SpecParity.Run(args);
