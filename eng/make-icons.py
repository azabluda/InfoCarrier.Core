# No `#!/usr/bin/env python3` shebang. Windows' `py` launcher READS the shebang and honours it, so
# that line makes the documented `py eng/make-icons.py` go looking for a `python3` that on a stock
# Windows box is the Microsoft Store stub -- it exits 49 without running anything.
"""Derive every shipped icon from the one source artwork.

    py eng/make-icons.py        # docs/assets/icon-source.png -> TARGETS, plus the .ico

WHY A SOURCE FILE AND A SCRIPT. None of the outputs is something anyone can edit or re-derive: a
128px PNG and a three-frame .ico are end products. Keeping the source beside them makes the shipped
set reproducible instead of a bag of one-offs, and it is what lets the *count* of shipped files be
argued about -- adding a size is a line in TARGETS, not another trip to a favicon generator.

THIS WAS CHECKED, NOT ASSUMED. The artwork arrived with a generator's full output beside it
(separate 16/32/48 PNGs, a 64px .ico, a duplicate apple-touch under its 2014 sized name). Rendering
each size here with the same fit-and-LANCZOS produced images indistinguishable from the generator's
at 16, 32, 48 and 128, so nothing was lost by dropping them and deriving instead.

WHAT SURVIVED THE CUT, AND WHY EACH ONE IS LOAD-BEARING:

  favicon.ico          the site's only declared `rel=icon`. Three frames, so the tab, the bookmark
                       bar and a Windows taskbar pin each get pixels drawn for their size rather
                       than one frame resampled by the browser. Separate 16/32/48 PNGs alongside it
                       would be three more `<link>` tags for pixels this file already carries.
  apple-touch-icon     iOS home screen. Safari does not read the web manifest for it, so this is
                       the one file no manifest entry can replace. 180 is the largest any iOS asks
                       for and every smaller device downscales it.
  icon-192, icon-512   Android home screen, and the install prompt's splash. Chrome wants one icon
                       at 192 or larger to consider a site installable and uses 512 for the splash.
  icon-64              the Blazor sample, serving its tab icon AND the mark in its breadcrumb from
                       one file. 64 covers a 32px tab at 2x and an 18px breadcrumb at 3x. No .ico
                       here: the sample is served by `dotnet run` to a modern browser, which reads
                       a PNG `rel=icon`, and nobody pins it to a taskbar.

  No SVG: the artwork is raster, lit and bevelled, and a traced approximation would be a different
  mark. No maskable variant: Android's maskable safe zone is the middle 80%, and this mark is
  already a rounded tile whose lit rim is the point of it -- padded to survive that crop it sits
  small enough inside Android's own tile to look like a mistake.

TRANSPARENCY IS KEPT EVERYWHERE, no invented background. iOS composites a transparent home-screen
icon on black, which the artwork's near-black rim and neon edge are already lit for -- it reads as
a tile. A white or navy plate was tried and both were worse: white shrinks the mark to fit its
padding, navy sits close enough to the mark's own fill to flatten it.

AND THE SOURCE ARRIVES AS ARTWORK, with a real alpha channel, which is why this script is a fit and
a resize rather than a flood fill. Earlier sources were browser screenshots -- square, opaque, white
behind the mark -- and cutting those meant flooding inward from the border (never keying white
globally, because the lettering is white too) and feathering the alpha by hand.
"""

import os

from PIL import Image

SRC = os.path.join("docs", "assets", "icon-source.png")

WEB = os.path.join("website", "docs", "assets", "icons")

SAMPLE = os.path.join("samples", "Northwind.Client", "wwwroot")

# (path, canvas, margin). The margin keeps the mark off the edge where something else frames it --
# a home-screen icon crops or rounds the corners. The favicon frames get none: at 16px a spare
# pixel of mark is worth more than breathing room nobody can see.
#
# NUGET IS THE ONE THAT LOOKS LIKE IT WANTS A MARGIN AND DOES NOT. nuget.org neither crops nor
# rounds -- it sets `width:32px;height:32px;object-fit:contain` on the package page and caps search
# rows at 60 -- so the margin there is pure inset. It is kept at 4 because 3% of 128 is invisible
# at 32 either way, and because Visual Studio's package list is not measured here.
TARGETS = [
    (os.path.join("docs", "assets", "icon.png"), 128, 4),        # nuget.org's recommended size
    (os.path.join(WEB, "apple-touch-icon.png"), 180, 6),
    (os.path.join(WEB, "icon-192.png"), 192, 6),
    (os.path.join(WEB, "icon-512.png"), 512, 16),
    (os.path.join(SAMPLE, "icon-64.png"), 64, 0),
]

ICO = os.path.join(WEB, "favicon.ico")
ICO_SIZES = [16, 32, 48]

# The alpha below which a pixel is empty space rather than part of the mark.
#
# `Image.getbbox()` ALONE IS NOT ENOUGH, and it fails silently. It trims only pixels that are zero
# in every band, so one row of alpha=1 -- invisible over any background, and exactly what a soft
# outer glow fades into -- holds the box open. A source arrived carrying that haze 55px past the
# mark on one side and 8px on another, and its icons came out both smaller than the canvas allows
# and visibly off-centre, because the padding left behind was uneven.
#
# 2 IS NOT A TUNED NUMBER, and that is the point: on that source the box moves at most 3px for any
# floor from 2 all the way to 128. Nothing between the haze and the mark is being cut, so the soft
# edge comes along, which is what keeps the glow on the rendered icon.
ALPHA_FLOOR = 2

# How far from square a mark may be and still be scaled to fill the canvas exactly.
#
# WHY NOT JUST PRESERVE THE ASPECT. Because the canvas is square and the leftover is not spread,
# it is a band. This artwork is 3.3% wider than tall, which sounds negligible and is not: at 16px
# it makes the mark 16x15 and leaves a whole empty ROW, 6% of the icon's height, and the centring
# puts all of it at the bottom because (16-15)//2 is 0. It is plainly visible in a browser tab.
# Every size pays it -- 1 row at 32, 2 at 48, 4 at 128, 17 at 512.
#
# 3.3% IS THE ARTWORK, NOT A STRAY GLOW PIXEL, which is why the trim above cannot fix it: the
# bounding box measures 1.0336 at alpha 2, 1.0328 at alpha 128 and 1.0319 at alpha 250. The tile
# itself is that shape.
#
# So the choice is a distortion or a band, and on a rounded tile a 3.3% stretch is invisible while
# the band is not. Cropping to square was the third option and was worse: it eats the lit rim on
# the left and right while leaving it on the top and bottom, which reads as a mistake at 48px.
#
# THE TOLERANCE IS THE GUARD. A mark further from square than this keeps its aspect and gets the
# band, because at that point the shape is the point of it -- a wordmark stretched 20% is a
# different wordmark.
SQUARE_TOLERANCE = 0.08


def render(mark, canvas, margin):
    """Fit the mark inside `canvas`, centred, leaving `margin` px on the tightest axis."""
    target = canvas - 2 * margin

    if abs(mark.width / mark.height - 1) <= SQUARE_TOLERANCE:
        size = (target, target)
    else:
        scale = min(target / mark.width, target / mark.height)
        size = (max(1, round(mark.width * scale)), max(1, round(mark.height * scale)))

    scaled = mark.resize(size, Image.LANCZOS)

    icon = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    icon.paste(scaled, ((canvas - size[0]) // 2, (canvas - size[1]) // 2), scaled)
    return icon


def main():
    src = Image.open(SRC).convert("RGBA")

    # Trim to what is actually visible, so every margin below is measured from the mark rather than
    # from however much transparent space the source happens to carry around it.
    visible = src.getchannel("A").point(lambda a: 255 if a >= ALPHA_FLOOR else 0)
    mark = src.crop(visible.getbbox())

    os.makedirs(WEB, exist_ok=True)

    for path, canvas, margin in TARGETS:
        render(mark, canvas, margin).save(path, optimize=True)
        print(f"{path:48s} {canvas}x{canvas}  {os.path.getsize(path)} bytes")

    # One .ico carrying a frame drawn for each size, rather than one frame the browser resamples.
    # Each frame is rendered from the source at its own size; Pillow stores them PNG-compressed.
    #
    # SAVED FROM THE LARGEST FRAME, and that is not a style choice. Pillow's ICO writer skips any
    # requested size wider or taller than the image `save` was called on, silently -- calling it on
    # the 16 wrote a one-frame 885-byte file that opens fine and looks right in a 16px tab.
    frames = {n: render(mark, n, 0) for n in ICO_SIZES}
    largest = frames.pop(max(frames))
    largest.save(ICO, sizes=[(n, n) for n in ICO_SIZES], append_images=list(frames.values()))

    written = sorted(n for n, _ in Image.open(ICO).ico.sizes())
    assert written == sorted(ICO_SIZES), f"{ICO} got frames {written}, wanted {sorted(ICO_SIZES)}"
    print(f"{ICO:48s} {'/'.join(str(n) for n in written)}  {os.path.getsize(ICO)} bytes")

    print(f"\nall from {SRC}  {src.size[0]}x{src.size[1]}, mark {mark.width}x{mark.height}"
          f" (trimmed at alpha {ALPHA_FLOOR})")


if __name__ == "__main__":
    main()
