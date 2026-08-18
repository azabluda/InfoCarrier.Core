#!/usr/bin/env python3
"""Derive the package icon and the documentation site's branding from the source artwork.

    python eng/make-icon.py

    docs/assets/icon-source.png  ->  docs/assets/icon.png            128x128, the nuget.org icon
                                 ->  website/docs/assets/logo.png    192x192, the site header
                                 ->  website/docs/assets/favicon.ico 16/32/48, the browser tab

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

AND THIS SOURCE ARRIVED WITH A REAL ALPHA CHANNEL, which is why this script is now a dozen lines of
work rather than a flood fill. Earlier sources were browser screenshots: square-ish, opaque, white
behind the mark. Cutting those meant flooding inward from the border -- never keying white
globally, because the lettering is white too -- and feathering the alpha by hand. None of that is
needed when the artwork is delivered as artwork.

WHY THE SITE ASSETS ARE GENERATED HERE RATHER THAN COPIED. MkDocs cannot reach outside its
`docs_dir`, so the site needs its own copy under `website/docs/assets/` no matter what. Deriving it
from the same source in the same run is what keeps the tab icon, the site header and the package
listing the same mark -- a copy would be a fourth artefact that drifts the first time one of them
is touched. And every size is resampled from the 1254px source INDEPENDENTLY: a 16px favicon cut
straight from the source is visibly cleaner than the browser's own downscale of a 128px PNG.
"""

import os

from PIL import Image

SRC = os.path.join("docs", "assets", "icon-source.png")

PACKAGE_ICON = os.path.join("docs", "assets", "icon.png")
SITE_LOGO = os.path.join("website", "docs", "assets", "logo.png")
SITE_FAVICON = os.path.join("website", "docs", "assets", "favicon.ico")

CANVAS = 128        # nuget.org's recommendation
MARGIN = 4          # keeps the mark off the edge at every display size

LOGO_CANVAS = 192   # the header renders it at ~24px; this is the 2x/3x display headroom
FAVICON_SIZES = (16, 32, 48)


def render(mark, canvas):
    """Fit the trimmed mark, centred, into a transparent square of `canvas` pixels."""
    target = canvas - 2 * round(canvas * MARGIN / CANVAS)
    scale = min(target / mark.width, target / mark.height)
    size = (max(1, round(mark.width * scale)), max(1, round(mark.height * scale)))
    resized = mark.resize(size, Image.LANCZOS)

    out = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    out.paste(resized, ((canvas - size[0]) // 2, (canvas - size[1]) // 2), resized)
    return out


def main():
    src = Image.open(SRC).convert("RGBA")

    # Trim to what is actually opaque, so the margin above is measured from the mark rather than
    # from however much transparent space the source happens to carry around it.
    mark = src.crop(src.getbbox())

    for path, canvas in ((PACKAGE_ICON, CANVAS), (SITE_LOGO, LOGO_CANVAS)):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        render(mark, canvas).save(path, optimize=True)
        print(f"{path}  {canvas}x{canvas}  {os.path.getsize(path)} bytes")

    # One .ico holding all three sizes, each resampled from the source rather than from each other.
    icons = [render(mark, n) for n in FAVICON_SIZES]
    icons[-1].save(SITE_FAVICON, sizes=[(n, n) for n in FAVICON_SIZES],
                   append_images=icons[:-1])
    print(f"{SITE_FAVICON}  {'/'.join(str(n) for n in FAVICON_SIZES)}  "
          f"{os.path.getsize(SITE_FAVICON)} bytes  from {src.size[0]}x{src.size[1]}")


if __name__ == "__main__":
    main()
