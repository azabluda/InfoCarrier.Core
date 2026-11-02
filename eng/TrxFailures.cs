#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/TrxFailures.cs

// Stream TRX results and print the sorted union of failed names, with LF line endings.
// Usage: <results.trx> [more.trx ...]. XML or input errors return 1; missing arguments return 2.
return TrxFailures.Run(args);
