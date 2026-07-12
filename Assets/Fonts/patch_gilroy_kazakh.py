"""
Copy missing Kazakh glyphs from NotoSans-Medium into Gilroy-Medium.
Decomposes composites to simple outlines and scales by cap-height ratio.
Output: Gilroy-Medium-KZ.ttf

Usage:  py -3 patch_gilroy_kazakh.py
"""
import pathlib
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

DIR = pathlib.Path(__file__).parent
GILROY_PATH = DIR / "Gilroy-Medium.ttf"
NOTO_PATH   = DIR / "NotoSans-Medium.ttf"
OUT_PATH    = DIR / "Gilroy-Medium-KZ.ttf"

KAZAKH_CODES = [
    0x04D8, 0x04D9,  # Әә
    0x0492, 0x0493,  # Ғғ
    0x049A, 0x049B,  # Ққ
    0x04A2, 0x04A3,  # Ңң
    0x04E8, 0x04E9,  # Өө
    0x04B0, 0x04B1,  # Ұұ
    0x04AE, 0x04AF,  # Үү
    0x04BA, 0x04BB,  # Һһ
]


def decompose_and_scale(noto_font, glyph_name, scale):
    """Decompose composite glyph to simple outlines and scale."""
    glyph_set = noto_font.getGlyphSet()

    # DecomposingRecordingPen flattens composites into simple drawing commands
    rec = DecomposingRecordingPen(glyph_set)
    glyph_set[glyph_name].draw(rec)

    scaled_ops = []
    for op, args in rec.value:
        if op in ("moveTo", "lineTo"):
            scaled_ops.append((op, ((round(args[0][0]*scale), round(args[0][1]*scale)),)))
        elif op in ("qCurveTo", "curveTo"):
            new_pts = tuple((round(x*scale), round(y*scale)) for x, y in args)
            scaled_ops.append((op, new_pts))
        else:
            # closePath, endPath
            scaled_ops.append((op, args))

    return scaled_ops


def main():
    gilroy = TTFont(str(GILROY_PATH))
    noto   = TTFont(str(NOTO_PATH))

    g_cap = gilroy["OS/2"].sCapHeight
    n_cap = noto["OS/2"].sCapHeight
    scale = g_cap / n_cap
    print(f"Scale factor: {scale:.4f} (Gilroy cap={g_cap}, Noto cap={n_cap})")

    g_cmap = gilroy.getBestCmap()
    n_cmap = noto.getBestCmap()
    g_hmtx = gilroy["hmtx"]
    n_hmtx = noto["hmtx"]
    g_glyf = gilroy["glyf"]

    # We need a glyphSet for TTGlyphPen to avoid component lookup issues
    # Since we fully decompose, there are no components - pass empty dict
    added = []
    for code in KAZAKH_CODES:
        if code in g_cmap:
            print(f"  U+{code:04X} already in Gilroy, skipping")
            continue
        if code not in n_cmap:
            print(f"  U+{code:04X} NOT in NotoSans, skipping")
            continue

        noto_name = n_cmap[code]
        glyph_name = f"uni{code:04X}"

        # Decompose and scale
        scaled_ops = decompose_and_scale(noto, noto_name, scale)

        # Build TrueType glyph (pass empty dict as glyphSet since no components)
        tt_pen = TTGlyphPen(glyphSet={})
        for op, args in scaled_ops:
            getattr(tt_pen, op)(*args)

        new_glyph = tt_pen.glyph()

        # Add to Gilroy
        gilroy.setGlyphOrder(gilroy.getGlyphOrder() + [glyph_name])
        g_glyf[glyph_name] = new_glyph

        # Scale advance width
        noto_aw, _ = n_hmtx[noto_name]
        lsb = new_glyph.xMin if hasattr(new_glyph, 'xMin') and new_glyph.numberOfContours > 0 else 0
        g_hmtx[glyph_name] = (int(round(noto_aw * scale)), lsb)

        # Map codepoint in all cmap tables
        for table in gilroy["cmap"].tables:
            if hasattr(table, "cmap") and table.cmap is not None:
                table.cmap[code] = glyph_name

        added.append(f"U+{code:04X} -> {glyph_name}")
        print(f"  + {glyph_name}")

    print(f"\nAdded {len(added)} glyphs")
    gilroy.save(str(OUT_PATH))
    print(f"Saved: {OUT_PATH}")
    print("\nNext steps in Unity:")
    print("1. Select G-Medium font asset")
    print("2. Change Source Font File to Gilroy-Medium-KZ.ttf")
    print("3. Click 'Generate Font Atlas'")

if __name__ == "__main__":
    main()
