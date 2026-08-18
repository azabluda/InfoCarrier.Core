#!/usr/bin/env python3
"""Derive the package icon from the source artwork.

    python eng/make-icon.py        # docs/assets/icon-source.png -> docs/assets/icon.png

WHY THERE IS A SOURCE FILE AND A SCRIPT. `docs/assets/icon.png` is 128x128 because that is what
nuget.org recommends, and a 128px PNG is not something anyone can edit or re-derive. The source it
comes from is kept beside it, so the shipped icon is reproducible rather than a one-off.

THE HISTORY IS WORTH ONE PARAGRAPH, because it is entirely about resolution. The first icon was
scaled up from a 94x104 hexagon in a 185x164 draft -- a 1.23x upscale, and it looked like one.
Rebuilding the mark from primitives (hexagon, arcs, circles, stem) produced a spike at the apex and
misplaced lettering. Cropping the mark out of the 1774x887 README banner at native resolution was
sharp but only 109x121, and masking it that tightly cut the rim highlight the artwork is lit by.
The source used now is ~654x763, so 128 is a 0.16x DOWNSCALE: the sampling does the anti-aliasing
and there is nothing to reconstruct.

HOW THE BACKGROUND IS REMOVED, and why not simply "white -> transparent". The lettering inside the
mark is white too. Flooding inward from the border only reaches pixels connected to the outside, so
the glyphs are never touched. Alpha is feathered by how close each pixel is to white, at FULL
resolution -- the 6x downscale afterwards is what actually resolves the edge, which is why no
supersampling appears anywhere in here.
"""

import os
from collections import deque

from PIL import Image

SRC = os.path.join("docs", "assets", "icon-source.png")
OUT = os.path.join("docs", "assets", "icon.png")

CANVAS = 128        # nuget.org's recommendation
MARGIN = 4          # keeps the mark off the edge at every display size
LOOSE = 200         # a pixel this pale, reachable from the border, is background


def main():
    src = Image.open(SRC).convert("RGBA")
    w, h = src.size
    px = src.load()

    def pale(p):
        return min(p[0], p[1], p[2]) > LOOSE

    seen = [[False] * h for _ in range(w)]
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if pale(px[x, y]) and not seen[x][y]:
                seen[x][y] = True
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if pale(px[x, y]) and not seen[x][y]:
                seen[x][y] = True
                q.append((x, y))

    while q:
        x, y = q.popleft()
        m = min(px[x, y][:3])
        # 255 -> fully clear, LOOSE -> nearly opaque: a soft edge rather than a cut
        px[x, y] = px[x, y][:3] + (max(0, min(255, int(255 * (255 - m) / (255 - LOOSE)))),)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and pale(px[nx, ny]):
                seen[nx][ny] = True
                q.append((nx, ny))

    # Trim to whatever is actually opaque, so the margin below is measured from the mark rather
    # than from however much whitespace the source happened to carry.
    bbox = src.getbbox()
    mark = src.crop(bbox)

    target = CANVAS - 2 * MARGIN
    scale = min(target / mark.width, target / mark.height)
    size = (round(mark.width * scale), round(mark.height * scale))
    mark = mark.resize(size, Image.LANCZOS)

    icon = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    icon.paste(mark, ((CANVAS - size[0]) // 2, (CANVAS - size[1]) // 2), mark)
    icon.save(OUT, optimize=True)
    print(f"{OUT}  {CANVAS}x{CANVAS}  mark {size[0]}x{size[1]}  {os.path.getsize(OUT)} bytes")


if __name__ == "__main__":
    main()
