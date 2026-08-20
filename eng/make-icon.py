#!/usr/bin/env python3
"""Derive the package icon from the source artwork.

    python eng/make-icon.py        # docs/assets/icon-source.png -> docs/assets/icon.png

WHY A SOURCE FILE AND A SCRIPT. `docs/assets/icon.png` is 128x128 because that is what nuget.org
recommends, and a 128px PNG is not something anyone can edit or re-derive. Keeping the source
beside it makes the shipped icon reproducible instead of a one-off.

THE ONLY THING THAT EVER MATTERED HERE WAS THE SOURCE. Four earlier icons were rejected and every
one failed the same way: the artwork was about the size of the output, so there was no headroom and
something had to be invented. A 94x104 hexagon scaled to 128 is a 1.23x upscale and looks like one.
Rebuilding the mark from primitives invented the shapes. A native-resolution crop of the README
banner was sharp but only 109x121, and masking it tightly enough to look neat cut the rim highlight
the artwork is lit by. The source used now is 1254x1254 with the mark at 1035x1184, so 128 is a
0.10x DOWNSCALE -- the resampling is the anti-aliasing, and nothing is reconstructed.

AND THIS SOURCE ARRIVED WITH A REAL ALPHA CHANNEL, which is why this script is now nine lines of
work rather than a flood fill. Earlier sources were browser screenshots: square-ish, opaque, white
behind the mark. Cutting those meant flooding inward from the border -- never keying white
globally, because the lettering is white too -- and feathering the alpha by hand. None of that is
needed when the artwork is delivered as artwork.
"""

import os

from PIL import Image

SRC = os.path.join("docs", "assets", "icon-source.png")
OUT = os.path.join("docs", "assets", "icon.png")

CANVAS = 128        # nuget.org's recommendation
MARGIN = 4          # keeps the mark off the edge at every display size


def main():
    src = Image.open(SRC).convert("RGBA")

    # Trim to what is actually opaque, so the margin below is measured from the mark rather than
    # from however much transparent space the source happens to carry around it.
    mark = src.crop(src.getbbox())

    target = CANVAS - 2 * MARGIN
    scale = min(target / mark.width, target / mark.height)
    size = (max(1, round(mark.width * scale)), max(1, round(mark.height * scale)))
    mark = mark.resize(size, Image.LANCZOS)

    icon = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    icon.paste(mark, ((CANVAS - size[0]) // 2, (CANVAS - size[1]) // 2), mark)
    icon.save(OUT, optimize=True)

    print(f"{OUT}  {CANVAS}x{CANVAS}  mark {size[0]}x{size[1]}  "
          f"from {src.size[0]}x{src.size[1]}  {os.path.getsize(OUT)} bytes")


if __name__ == "__main__":
    main()
