"""
Draw the missing tenge sign U+20B8 into TT Rounds Neue.

TT Rounds Neue ships without ₸, and the app prices in KZT, so every price line
fell back to another family for that one glyph. ₸ is simply a T with a second
horizontal bar above the crossbar, so instead of importing a foreign glyph the
bar is built from the font's OWN T: same stroke weight, same rounded terminals,
same advance. Nothing is copied from another typeface.

Output: <name>-KZ.ttf next to the source, originals untouched.

Usage:  py -3 patch_ttrounds_tenge.py
"""
import math
import pathlib

from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

DIR = pathlib.Path(__file__).parent
SOURCES = ["TTRoundsNeue-ExtraBold.ttf", "TTRoundsNeue-Bold.ttf", "TTRoundsNeue-Medium.ttf"]

TENGE = 0x20B8
LETTER_T = 0x0054

# Vertical gap between the two bars, as a share of a bar's thickness. Measured off
# Montserrat-Bold, which ships a real tenge: bars 84 units, gap 58 -> 0.69.
GAP_RATIO = 0.69
# Used only if the crossbar cannot be measured from the outline.
FALLBACK_THICKNESS_RATIO = 0.12


def record(font, glyph_name):
    """Flatten a glyph (composites included) into drawing commands."""
    glyph_set = font.getGlyphSet()
    pen = DecomposingRecordingPen(glyph_set)
    glyph_set[glyph_name].draw(pen)
    return pen.value


def all_points(ops):
    pts = []
    for op, args in ops:
        for a in args:
            if isinstance(a, tuple) and len(a) == 2 and isinstance(a[0], (int, float)):
                pts.append(a)
            elif isinstance(a, (list, tuple)):
                pts.extend(p for p in a if p is not None)
    return [p for p in pts if p is not None]


def signed_area(pts):
    """Positive = counter-clockwise in font (y-up) coordinates."""
    area = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % len(pts)]
        area += x0 * y1 - x1 * y0
    return area / 2.0


def flatten(ops, steps=24):
    """Drawing commands -> closed polygons, curves sampled into segments."""
    polys, cur = [], []
    for op, args in ops:
        if op == "moveTo":
            if cur:
                polys.append(cur)
            cur = [args[0]]
        elif op == "lineTo":
            cur.append(args[0])
        elif op == "qCurveTo":
            pts = list(args)
            on, offs, p0 = pts[-1], pts[:-1], cur[-1]
            seq = []
            for i, c in enumerate(offs):
                if i + 1 < len(offs):
                    nxt = offs[i + 1]
                    seq.append((c, ((c[0] + nxt[0]) / 2.0, (c[1] + nxt[1]) / 2.0)))
                else:
                    seq.append((c, on))
            for c, end in seq:
                for s in range(1, steps + 1):
                    t = s / steps
                    cur.append((
                        (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * end[0],
                        (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * end[1]))
                p0 = end
        elif op == "curveTo":
            c1, c2, end = args[0], args[1], args[2]
            p0 = cur[-1]
            for s in range(1, steps + 1):
                t, mt = s / steps, 1 - s / steps
                cur.append((
                    mt**3*p0[0] + 3*mt*mt*t*c1[0] + 3*mt*t*t*c2[0] + t**3*end[0],
                    mt**3*p0[1] + 3*mt*mt*t*c1[1] + 3*mt*t*t*c2[1] + t**3*end[1]))
        elif op in ("closePath", "endPath"):
            if cur:
                polys.append(cur)
                cur = []
    if cur:
        polys.append(cur)
    return polys


def span_at_x(polys, x):
    """Tallest y-interval the vertical line at x passes through inside the shape."""
    ys = []
    for poly in polys:
        for i in range(len(poly)):
            x0, y0 = poly[i]
            x1, y1 = poly[(i + 1) % len(poly)]
            if (x0 <= x < x1) or (x1 <= x < x0):
                ys.append(y0 + (x - x0) / (x1 - x0) * (y1 - y0))
    ys.sort()
    spans = [(ys[i], ys[i + 1]) for i in range(0, len(ys) - 1, 2)]
    return max(spans, key=lambda s: s[1] - s[0]) if spans else None


def measure_crossbar(ops):
    """
    The T's crossbar, read across the outline rather than from its corner points:
    TT Rounds finishes the bar with a semicircular terminal, so the extreme left
    point sits at mid-height and would report the bar as much thinner than it is.
    A quarter of the way in, the bar is at full thickness and clear of the stem.
    """
    polys = flatten(ops)
    xs = [p[0] for poly in polys for p in poly]
    ys = [p[1] for poly in polys for p in poly]
    x_min, x_max, y_max = min(xs), max(xs), max(ys)
    span = span_at_x(polys, x_min + (x_max - x_min) * 0.25)
    return x_min, x_max, y_max, (span[0] if span else None)


def scale_y(ops, s):
    """
    Squash drawing commands vertically about the baseline.

    Vertical only, deliberately: it leaves the stem's width — the heaviest stroke in
    the sign, and the one read against the digits beside it — untouched, and thins
    only the horizontals. That is the same trade a type designer makes here, since
    the sign stacks two bars where the letter has one.
    """
    out = []
    for op, args in ops:
        if op in ("moveTo", "lineTo"):
            out.append((op, ((args[0][0], args[0][1] * s),)))
        elif op in ("qCurveTo", "curveTo"):
            out.append((op, tuple((x, y * s) for x, y in args)))
        else:
            out.append((op, args))
    return out


def arc(cx, cy, r, a0, a1, steps=4):
    """Quadratic segments approximating an arc; control = tangent intersection."""
    ops = []
    for i in range(steps):
        t0 = a0 + (a1 - a0) * i / steps
        t1 = a0 + (a1 - a0) * (i + 1) / steps
        tm = (t0 + t1) / 2.0
        d = r / math.cos((t1 - t0) / 2.0)
        ctrl = (cx + d * math.cos(tm), cy + d * math.sin(tm))
        end = (cx + r * math.cos(t1), cy + r * math.sin(t1))
        ops.append(("qCurveTo", ((round(ctrl[0]), round(ctrl[1])),
                                 (round(end[0]), round(end[1])))))
    return ops


def stadium(x_left, x_right, y_bottom, y_top, clockwise):
    """A bar with fully rounded ends — the terminal shape TT Rounds uses."""
    r = (y_top - y_bottom) / 2.0
    cy = (y_top + y_bottom) / 2.0
    lcx, rcx = x_left + r, x_right - r
    ops = []
    if clockwise:
        ops.append(("moveTo", ((round(lcx), round(y_top)),)))
        ops.append(("lineTo", ((round(rcx), round(y_top)),)))
        ops += arc(rcx, cy, r, math.pi / 2, -math.pi / 2)
        ops.append(("lineTo", ((round(lcx), round(y_bottom)),)))
        ops += arc(lcx, cy, r, -math.pi / 2, -3 * math.pi / 2)
    else:
        ops.append(("moveTo", ((round(lcx), round(y_bottom)),)))
        ops.append(("lineTo", ((round(rcx), round(y_bottom)),)))
        ops += arc(rcx, cy, r, -math.pi / 2, math.pi / 2)
        ops.append(("lineTo", ((round(lcx), round(y_top)),)))
        ops += arc(lcx, cy, r, math.pi / 2, 3 * math.pi / 2)
    ops.append(("closePath", ()))
    return ops


def patch(source_name):
    src = DIR / source_name
    out = DIR / (src.stem + "-KZ.ttf")
    font = TTFont(str(src))

    cmap = font.getBestCmap()
    if TENGE in cmap:
        print(f"{source_name}: U+20B8 already present, nothing to do")
        return
    if LETTER_T not in cmap:
        print(f"{source_name}: no T to build from, skipping")
        return

    t_name = cmap[LETTER_T]
    t_ops = record(font, t_name)
    x_min, x_max, y_max, bar_bottom = measure_crossbar(t_ops)

    cap = font["OS/2"].sCapHeight or y_max
    thickness = y_max - bar_bottom if bar_bottom is not None else 0
    if not 0 < thickness < cap * 0.5:
        thickness = cap * FALLBACK_THICKNESS_RATIO
        print(f"  crossbar not measurable, using {thickness:.0f} units")

    # The whole sign has to end at cap height, not tower a third above it the way a
    # full-height T with a bar stacked on top does. So the T is squashed until it,
    # the gap and the added bar add up to exactly the cap:
    #     cap*s + GAP_RATIO*(t*s) + t*s = cap
    # and the bar then takes the squashed crossbar's thickness, so both bars match.
    s = cap / (cap + thickness * (1.0 + GAP_RATIO))
    bar = thickness * s
    gap = bar * GAP_RATIO

    t_ops = scale_y(t_ops, s)
    y_top = y_max * s            # top of the squashed crossbar
    y0 = y_top + gap
    y1 = y0 + bar

    clockwise = signed_area(all_points(t_ops)) < 0
    ops = list(t_ops) + stadium(x_min, x_max, y0, y1, clockwise)

    pen = TTGlyphPen(glyphSet={})
    for op, args in ops:
        getattr(pen, op)(*args)
    glyph = pen.glyph()

    name = "uni20B8"
    font.setGlyphOrder(font.getGlyphOrder() + [name])
    font["glyf"][name] = glyph
    glyph.recalcBounds(font["glyf"])

    advance, _ = font["hmtx"][t_name]
    font["hmtx"][name] = (advance, glyph.xMin)

    mapped = 0
    for table in font["cmap"].tables:
        if getattr(table, "cmap", None) is not None:
            table.cmap[TENGE] = name
            mapped += 1

    font.save(str(out))
    print(f"{source_name}: T crossbar {thickness:.0f}, squashed x{s:.3f} -> "
          f"bars {bar:.0f}, gap {gap:.0f}, top {y1:.0f} (cap {cap})")
    print(f"  + {name} in {mapped} cmap subtable(s), advance={advance}")
    print(f"  saved {out.name}")


if __name__ == "__main__":
    for s in SOURCES:
        patch(s)
    print("\nNext: point the TMP font assets at the -KZ files and rebuild their atlas.")
