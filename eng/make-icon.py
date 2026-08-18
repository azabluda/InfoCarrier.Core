#!/usr/bin/env python3
"""Cut the package icon out of the banner artwork.

    python eng/make-icon.py        # writes docs/assets/icon.png

WHY A CROP AND NOT A REDRAW. Two earlier attempts failed and both are worth knowing about. Scaling
the 94x104 hexagon out of the original 185x164 draft up to 128 is a 1.23x upscale and looks like
one. Rebuilding the mark from primitives -- it is only a hexagon, two arcs, three circles and a
stem -- produced a spike at the apex and misplaced lettering; it would have been a reconstruction
of intent from 94 pixels rather than the design.

`docs/assets/infocarrier-core-banner.png` is 1774x887 and carries a better variant of the mark at
109x121.5 (note its `i` dot is WHITE, where the draft's is blue). A 128x128 crop around it involves
NO RESAMPLING AT ALL, which is the only way to be sure the icon is as sharp as the source.

WHY THE MASK IS COMPUTED RATHER THAN DRAWN. The plate carries a light-to-shade gradient that has to
survive, so only the silhouette may be cut. Pillow has no rounded polygon, and the usual trick --
fill an inset polygon, then stroke it with a round-jointed pen -- leaves a visible notch where the
stroke closes, at the top apex. An analytic signed distance field has no seam: erode every edge by
the corner radius and dilate by the same amount, and the edges stay put while the corners round.

The numbers below were measured off the banner from luminance profiles across each edge, not
guessed. INSET exists because the outermost pixel of the plate is already blended with the black
field behind it; taking it would leave a dark fringe.

INSET IS 1.0 AND NOT 2.0, WHICH IS NOT A ROUNDING PREFERENCE. The plate is lit from the top left
and carries a rim highlight on its upper-left edges only -- about twice the brightness of the plate
(left edge 56, top apex 62, against a plate around 29), while the right and bottom edges have none.
That rim IS the light effect. At INSET=2.0 the mask boundary falls just inside it and the highlight
is cut to alpha 0 on every edge that has one. The result still looks tidy, which is exactly why it
survived a review that only asked whether the edge was clean. Measured at identified rim pixels:

    inset   (584,51)  (581,57)  (581,69)  (581,105)
      1.0        255       255       255        255
      2.0        132         0         0          0

1.0 keeps the highlight and costs one near-black pixel on the entire boundary.
"""

import math
import os

from PIL import Image

BANNER = os.path.join("docs", "assets", "infocarrier-core-banner.png")
OUT = os.path.join("docs", "assets", "icon.png")

CX, CY = 634.5, 82.25      # plate centre in the banner
W, H = 109.0, 121.5        # plate size; aspect 0.897, a little wider than a regular hexagon
BOX = (571, 18, 699, 146)  # the 128x128 crop, chosen so the plate lands centred
INSET = 1.0                # clears the background-blended pixel, keeps the rim highlight
RADIUS = 16.0              # corner rounding, matched to the artwork
SIZE = 128                 # what nuget.org recommends


def hexagon(cx, cy, w, h):
    """Pointy-top hexagon: a vertex top and bottom, vertical left and right sides."""
    return [(cx, cy - h / 2), (cx + w / 2, cy - h / 4), (cx + w / 2, cy + h / 4),
            (cx, cy + h / 2), (cx - w / 2, cy + h / 4), (cx - w / 2, cy - h / 4)]


def half_planes(pts):
    """Outward unit normal and offset per edge, which is all a convex SDF needs."""
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    out = []
    for i in range(len(pts)):
        (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
        nx, ny = (y1 - y0), -(x1 - x0)
        n = math.hypot(nx, ny)
        nx, ny = nx / n, ny / n
        if nx * (cx - x0) + ny * (cy - y0) > 0:
            nx, ny = -nx, -ny
        out.append((nx, ny, nx * x0 + ny * y0))
    return out


def main():
    banner = Image.open(BANNER).convert("RGB")
    crop = banner.crop(BOX)
    assert crop.size == (SIZE, SIZE), crop.size

    planes = half_planes(hexagon(CX - BOX[0], CY - BOX[1], W - 2 * INSET, H - 2 * INSET))

    alpha = Image.new("L", (SIZE, SIZE), 0)
    ap = alpha.load()
    for y in range(SIZE):
        for x in range(SIZE):
            px, py = x + 0.5, y + 0.5
            distance = max(nx * px + ny * py - (d - RADIUS) for nx, ny, d in planes) - RADIUS
            # 0.5 - distance gives one pixel of linear coverage across the boundary: an
            # anti-aliased edge without supersampling anything.
            ap[x, y] = int(max(0.0, min(1.0, 0.5 - distance)) * 255)

    icon = crop.convert("RGBA")
    icon.putalpha(alpha)
    icon.save(OUT, optimize=True)
    print(f"{OUT}  {icon.size[0]}x{icon.size[1]}  {os.path.getsize(OUT)} bytes")


if __name__ == "__main__":
    main()
