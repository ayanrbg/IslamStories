"""
Builds Gilroy-Medium-KZ.ttf: Gilroy-Medium plus the Kazakh letters it lacks, drawn from
Gilroy's OWN glyphs, the way a type designer would extend it:

    Қ қ = К к + the tail of Ц ц      Ң ң = Н н + the tail of Ц ц
    Ғ ғ = Г г + a bar                Ө ө = О о + a bar
    Ү   = Y                          ү   = v + a descending stem
    Ұ ұ = Ү ү + a bar                Һ   = Ч turned 180°,  һ = h
    Ә   = Э closed at the bottom     ә   = е turned 180°

Bars are as thick as Gilroy's crossbars (Н н) and sit where Noto Sans puts them.
The previous version pasted Noto Sans glyphs in: another design, stroke and width, and
every one of them pushed to the left edge of its box — Kazakh words looked uneven.

Needs:  py -m pip install fonttools skia-pathops
Usage:  py -3 patch_gilroy_kazakh.py
Then in Unity: FairyTales > Rebuild Font Fallbacks is not needed (the dynamic Gilroy
fallbacks read this file), but clear their atlases so old glyphs are re-rendered.
"""
import pathlib
import pathops
from fontTools.ttLib import TTFont
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

DIR = pathlib.Path(__file__).parent
SRC = DIR / "Gilroy-Medium.ttf"
OUT = DIR / "Gilroy-Medium-KZ.ttf"

font = TTFont(str(SRC))
cmap = font.getBestCmap()
glyphs = font.getGlyphSet()
hmtx = font["hmtx"]


def outline(ch, transform=None):
    """A glyph of Gilroy as a pathops path, optionally transformed."""
    path = pathops.Path()
    pen = path.getPen(glyphSet=glyphs)
    glyphs[cmap[ord(ch)]].draw(TransformPen(pen, transform) if transform else pen)
    return path


def rect(x0, y0, x1, y1):
    path = pathops.Path()
    pen = path.getPen()
    pen.moveTo((x0, y0)); pen.lineTo((x1, y0)); pen.lineTo((x1, y1)); pen.lineTo((x0, y1))
    pen.closePath()
    return path


def union(*paths):
    result = paths[0]
    for p in paths[1:]:
        result = pathops.op(result, p, pathops.PathOp.UNION)
    return result


def advance(ch):
    return hmtx[cmap[ord(ch)]][0]


# Ц/ц: the tail is a stem-wide block hanging below the right stem, overlapping it a little.
def tail_upper(stem_right):   # Ц: stem ends at 583, block 565..658 × -134..88
    return rect(stem_right - 18, -134, stem_right + 75, 88)


def tail_lower(stem_right):   # ц: stem ends at 486, block 482..568 × -134..83
    return rect(stem_right - 4, -134, stem_right + 82, 83)


def turned(ch):
    """The glyph turned 180° inside its own box."""
    x0, y0, x1, y1 = outline(ch).bounds
    return outline(ch, (-1, 0, 0, -1, x0 + x1, y0 + y1))


def clip(path, x0, y0, x1, y1):
    return pathops.op(path, rect(x0, y0, x1, y1), pathops.PathOp.INTERSECTION)


def schwa_upper():
    """
    Э closed into a bowl: its top and lower right as drawn, its lower left the mirror of
    the lower right (bar included). Э's bowl is centred on x=335, so the result is as wide
    as О; shifted to О's side bearing.
    """
    centre, bar_top = 335, 394
    e = outline("Э")
    mirrored = outline("Э", (-1, 0, 0, 1, 2 * centre, 0))
    bowl = union(clip(e, -500, bar_top, 1500, 1000),
                 clip(e, centre, -500, 1500, bar_top),
                 clip(mirrored, -500, -500, centre, bar_top))
    shifted = pathops.Path()
    bowl.draw(TransformPen(shifted.getPen(), (1, 0, 0, 1, 38 - bowl.bounds[0], 0)))
    return shifted


V_STEM = rect(216, -200, 302, 40)   # ү: v's vertex is at x≈259; stem as wide as н's (87)

# Advance: a letter to copy it from, or the right side bearing after the outline — the
# tails reach past the stem, and Ц/ц keep 32/27 units clear after theirs.
KAZAKH = {
    0x049A: (union(outline("К"), tail_upper(584)), 32),      # Қ
    0x049B: (union(outline("к"), tail_lower(489)), 27),      # қ
    0x04A2: (union(outline("Н"), tail_upper(600)), 32),      # Ң
    0x04A3: (union(outline("н"), tail_lower(499)), 27),      # ң
    0x0492: (union(outline("Г"), rect(13, 302, 365, 390)), "Г"),   # Ғ
    0x0493: (union(outline("г"), rect(5, 204, 321, 288)), "г"),    # ғ
    0x04E8: (union(outline("О"), rect(60, 312, 741, 400)), "О"),   # Ө
    0x04E9: (union(outline("о"), rect(60, 214, 543, 297)), "о"),   # ө
    0x04AE: (outline("Y"), "Y"),                                   # Ү
    0x04AF: (union(outline("v"), V_STEM), "v"),                    # ү
    0x04B0: (union(outline("Y"), rect(96, 157, 507, 245)), "Y"),   # Ұ
    0x04B1: (union(outline("v"), V_STEM, rect(77, -79, 440, 4)), "v"),  # ұ
    0x04BA: (turned("Ч"), "Ч"),                                    # Һ
    0x04BB: (outline("h"), "h"),                                   # һ
    0x04D8: (schwa_upper(), "О"),                                  # Ә
    0x04D9: (turned("е"), "е"),                                    # ә
}

order = font.getGlyphOrder()
for code, (path, width_of) in KAZAKH.items():
    name = f"uni{code:04X}"
    tt = TTGlyphPen(glyphSet=None)
    path.draw(Cu2QuPen(tt, max_err=1.0, reverse_direction=False))
    glyph = tt.glyph()
    glyph.recalcBounds(font["glyf"])
    if name not in order:
        order.append(name)
    font["glyf"][name] = glyph
    width = glyph.xMax + width_of if isinstance(width_of, int) else advance(width_of)
    hmtx[name] = (width, glyph.xMin if glyph.numberOfContours else 0)
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[code] = name

font.setGlyphOrder(order)
for stale in ("hdmx", "LTSH", "VDMX"):   # per-size caches that would now be wrong
    if stale in font:
        del font[stale]
font.save(str(OUT))
print(f"Saved {OUT.name}: {len(KAZAKH)} Kazakh letters built from Gilroy")
