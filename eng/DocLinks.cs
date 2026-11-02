#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:include commands/DocLinks.cs

// Check local Markdown targets and heading anchors. Usage: [file ...]; no arguments checks all active docs.
// Broken links return 1. The testable parser is in commands/DocLinks.cs.
return DocLinks.Run(args);
