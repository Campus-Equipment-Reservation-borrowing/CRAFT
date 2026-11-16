"""CRAFT brand kit generator, concept A "Return Loop".
Usage: python3 -I gen_brand.py <out_dir>
"""
import math, os, sys

OUT = sys.argv[1]
C = 32.0
FONT = "Inter, Manrope, 'DM Sans', 'Segoe UI', Helvetica, Arial, sans-serif"
TITLE = "CRAFT: Campus Reservation and Asset Flow Tracker"
TAGLINE = "CAMPUS RESERVATION · ASSET FLOW TRACKER"

# palette (all taken from wwwroot/css/tokens.css)
GREEN, GREEN_DARK, MINT, MINT_2, GOLD, SLATE = "#168A5B", "#0B5D3E", "#A9D8C2", "#CEE7D9", "#E8C87E", "#7C8A83"

f = lambda p: f"{p[0]:.2f} {p[1]:.2f}"
def pt(r, deg):
    a = math.radians(deg)
    return (C + r * math.cos(a), C + r * math.sin(a))
poly = lambda *ps: "M" + " L".join(f(p) for p in ps) + " Z"


def geometry(R=21.0, SW=5.2, start=-50, end=58, alen=8.6, aw=5.3, k=10.6, node_deg=-24, node_r=3.7):
    """Ring (C) with arrowhead, isometric cube, gold tracking node."""
    s, e = pt(R, start), pt(R, end)
    th = math.radians(end)
    tan, nrm = (math.sin(th), -math.cos(th)), (math.cos(th), math.sin(th))
    base = (e[0] - tan[0] * 1.2, e[1] - tan[1] * 1.2)
    tip = (e[0] + tan[0] * alen, e[1] + tan[1] * alen)
    b1 = (base[0] + nrm[0] * aw, base[1] + nrm[1] * aw)
    b2 = (base[0] - nrm[0] * aw, base[1] - nrm[1] * aw)
    h = k * math.sqrt(3) / 2
    T, UR, LR, B = (C, C - k), (C + h, C - k / 2), (C + h, C + k / 2), (C, C + k)
    LL, UL, M = (C - h, C + k / 2), (C - h, C - k / 2), (C, C)
    return dict(arc=f"M{f(s)} A{R} {R} 0 1 0 {f(e)}", sw=SW, arrow=poly(tip, b1, b2),
                top=poly(T, UR, M, UL), left=poly(UL, M, B, LL), right=poly(M, UR, LR, B),
                node=pt(R, node_deg), node_r=node_r)


STD = geometry()
SMALL = geometry(R=20.5, SW=7.0, start=-52, end=60, alen=9.6, aw=6.6, k=12.4, node_deg=-22, node_r=5.0)


def glyph(g, ring, top, left, right, gold=GOLD, left_op=1, right_op=1):
    op = lambda o: f' fill-opacity="{o}"' if o != 1 else ""
    n = g["node"]
    return (f'<path d="{g["arc"]}" fill="none" stroke="{ring}" stroke-width="{g["sw"]}" stroke-linecap="round"/>\n'
            f'  <path d="{g["arrow"]}" fill="{ring}" stroke="{ring}" stroke-width="1.2" stroke-linejoin="round"/>\n'
            f'  <path d="{g["top"]}" fill="{top}"/>\n'
            f'  <path d="{g["left"]}" fill="{left}"{op(left_op)}/>\n'
            f'  <path d="{g["right"]}" fill="{right}"{op(right_op)}/>\n'
            f'  <circle cx="{n[0]:.2f}" cy="{n[1]:.2f}" r="{g["node_r"]}" fill="{gold}"/>')


DEFS = ('<defs><linearGradient id="craftBg" x1="6" y1="4" x2="58" y2="62" gradientUnits="userSpaceOnUse">'
        f'<stop offset="0" stop-color="{GREEN}"/><stop offset="1" stop-color="{GREEN_DARK}"/></linearGradient>'
        '<linearGradient id="craftPanel" x1="0" y1="0" x2="1" y2="1">'
        f'<stop offset="0" stop-color="{GREEN}"/><stop offset=".7" stop-color="{GREEN_DARK}"/></linearGradient></defs>')
TILE = '<rect width="64" height="64" rx="16" fill="url(#craftBg)"/>'
SQUARE = '<rect width="64" height="64" fill="url(#craftBg)"/>'  # full-bleed, for OS masks (iOS / Android)

ON_DARK = glyph(STD, "#FFFFFF", "#FFFFFF", MINT, MINT_2)
ON_LIGHT = glyph(STD, GREEN, MINT, GREEN_DARK, GREEN)
mono = lambda c: glyph(STD, c, c, c, c, gold=c, left_op=.72, right_op=.86)
ICON = f"{TILE}\n  {ON_DARK}"
ICON_SMALL = f"{TILE}\n  {glyph(SMALL, '#FFFFFF', '#FFFFFF', MINT, MINT_2)}"
ICON_BLEED = f'{SQUARE}\n  <g transform="translate(6.4 6.4) scale(.8)">{ON_DARK}</g>'


def svg(vb, body, title="CRAFT"):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{vb}" role="img" aria-labelledby="t">\n'
            f'  <title id="t">{title}</title>\n  {DEFS}\n  {body}\n</svg>\n')


def wordmark(x, y, name, sub, accent, anchor="start", size=34, sub_size=7.6, gap=12):
    """CRAFT + gold rule + tagline. Tagline width is pinned with textLength so it holds in any font."""
    if anchor == "middle":
        rule = f'<rect x="{x - 12}" y="{y + 8}" width="24" height="3" rx="1.5" fill="{accent}"/>'
        sub_el = (f'<text x="{x}" y="{y + 8 + gap + 7}" text-anchor="middle" font-family="{FONT}" font-size="{sub_size}" '
                  f'font-weight="700" textLength="214" lengthAdjust="spacing" fill="{sub}">{TAGLINE}</text>')
    else:
        rule = f'<rect x="{x + 1}" y="{y + 8}" width="22" height="3" rx="1.5" fill="{accent}"/>'
        sub_el = (f'<text x="{x + 29}" y="{y + 12}" font-family="{FONT}" font-size="{sub_size}" font-weight="700" '
                  f'textLength="226" lengthAdjust="spacing" fill="{sub}">{TAGLINE}</text>')
    return (f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="{FONT}" font-size="{size}" font-weight="800" '
            f'letter-spacing="-0.8" fill="{name}">CRAFT</text>\n  {rule}\n  {sub_el}')


def horizontal(mark, name, sub, accent, bg=None):
    bgr = f'<rect width="372" height="80" rx="18" fill="{bg}"/>\n  ' if bg else ""
    return svg("0 0 372 80", f'{bgr}<g transform="translate(8 8)">{mark}</g>\n  {wordmark(88, 45, name, sub, accent)}', TITLE)


def stacked(mark, name, sub, accent, bg=None):
    bgr = f'<rect width="260" height="200" rx="22" fill="{bg}"/>\n  ' if bg else ""
    return svg("0 0 260 200", f'{bgr}<g transform="translate(82 14) scale(1.5)">{mark}</g>\n  '
               f'{wordmark(130, 154, name, sub, accent, anchor="middle", size=38, sub_size=8.2)}', TITLE)


files = {
    # symbol
    "craft-mark.svg": svg("0 0 64 64", ICON),                    # primary app icon
    "craft-mark-light.svg": svg("0 0 64 64", ON_LIGHT),          # full colour, no tile, for white backgrounds
    "craft-mark-mono.svg": svg("0 0 64 64", mono(GREEN_DARK)),   # one colour green
    "craft-mark-black.svg": svg("0 0 64 64", mono("#17251E")),   # one colour ink, for print / fax / stamps
    "craft-mark-white.svg": svg("0 0 64 64", mono("#FFFFFF")),   # knockout for photos / dark green
    "craft-favicon.svg": svg("0 0 64 64", ICON_SMALL),           # heavier strokes for 16-32 px
    "craft-icon-maskable.svg": svg("0 0 64 64", ICON_BLEED),     # full-bleed square, symbol inside safe zone
    # horizontal lockups
    "craft-logo.svg": horizontal(ICON, GREEN_DARK, SLATE, GOLD),
    "craft-logo-reverse.svg": horizontal(ON_DARK, "#FFFFFF", MINT, GOLD, bg="url(#craftPanel)"),
    "craft-logo-white.svg": horizontal(mono("#FFFFFF"), "#FFFFFF", "#FFFFFF", "#FFFFFF"),
    "craft-logo-mono.svg": horizontal(mono(GREEN_DARK), GREEN_DARK, GREEN_DARK, GREEN_DARK),
    # stacked lockups
    "craft-logo-stacked.svg": stacked(ICON, GREEN_DARK, SLATE, GOLD),
    "craft-logo-stacked-reverse.svg": stacked(ON_DARK, "#FFFFFF", MINT, GOLD, bg="url(#craftPanel)"),
    # wordmark only
    "craft-wordmark.svg": svg("0 0 284 64", wordmark(2, 38, GREEN_DARK, SLATE, GOLD), TITLE),
}
for name, content in files.items():
    with open(os.path.join(OUT, name), "w") as fh:
        fh.write(content)
print("\n".join(files))
