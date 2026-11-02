#!/usr/bin/env -S dotnet --
// Licensed under the MIT license. See license.txt file in the project root for license information.

#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Linux.NoDependencies
#:include commands/MakeIcons.cs

// Generate five PNGs and a three-frame ICO from docs/assets/icon-source.png.
// Usage: [--output-root directory]. Redirect outputs for review without replacing shipped artwork.
return MakeIcons.Run(args);
