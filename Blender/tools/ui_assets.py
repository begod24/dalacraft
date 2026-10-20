"""Kazakh Mine — pixel UI sprites (HUD, menus, inventory) in Kazakh felt/ornament style.
Run inside Blender after km_tools.py:  exec(open(".../Blender/tools/ui_assets.py").read())
Writes PNGs to Assets/Resources/UI/.
"""
import bpy, math, os
import numpy as np

K = bpy.app.driver_namespace['km']; hsh = K['hsh']
OUT = K['UA'] + "/Resources/UI"
os.makedirs(OUT, exist_ok=True)

RED = (0.70, 0.13, 0.11); REDD = (0.45, 0.07, 0.06); GOLD = (0.93, 0.72, 0.22); GOLDD = (0.62, 0.42, 0.10); GOLDL = (1.0, 0.90, 0.55)
FELT = (0.93, 0.89, 0.78); FELTD = (0.78, 0.72, 0.60); DARK = (0.16, 0.11, 0.09); DARK2 = (0.24, 0.17, 0.13); BLUE = (0.16, 0.32, 0.62)
TURQ = (0.20, 0.72, 0.74); TURQD = (0.08, 0.40, 0.45); WHITE = (0.98, 0.97, 0.94); BLACK = (0.05, 0.04, 0.04)


class Img:
    def __init__(s, w, h): s.w, s.h = w, h; s.a = np.zeros((h, w, 4), dtype=np.float32)
    def px(s, x, y, c, a=1.0):
        # (x, y) with y=0 at the TOP (screen convention); stored flipped for Blender
        if 0 <= x < s.w and 0 <= y < s.h: s.a[s.h - 1 - y, x] = (c[0], c[1], c[2], a)
    def rect(s, x, y, w, h, c, a=1.0):
        for i in range(w):
            for j in range(h): s.px(x + i, y + j, c, a)
    def frame(s, x, y, w, h, c):
        for i in range(w): s.px(x + i, y, c); s.px(x + i, y + h - 1, c)
        for j in range(h): s.px(x, y + j, c); s.px(x + w - 1, y + j, c)
    def noise_fill(s, x, y, w, h, c, n=0.04, seed=0):
        for i in range(w):
            for j in range(h):
                k = hsh(i, j, seed) * n * 2; s.px(x + i, y + j, (c[0] + k, c[1] + k, c[2] + k))
    def save(s, name):
        img = bpy.data.images.get(name) or bpy.data.images.new(name, s.w, s.h, alpha=True)
        if tuple(img.size) != (s.w, s.h): img.scale(s.w, s.h)
        img.alpha_mode = 'STRAIGHT'; img.pixels.foreach_set(s.a.ravel())
        img.filepath_raw = f"{OUT}/{name}.png"; img.file_format = 'PNG'; img.save()


def koshkar_corner(im, x, y, c, flipx=False, flipy=False):
    pts = [(0, 0), (1, 0), (2, 0), (0, 1), (0, 2), (2, 1), (1, 2), (3, 1), (1, 3)]
    for (dx, dy) in pts: im.px(x + (-dx if flipx else dx), y + (-dy if flipy else dy), c)


def ornate_box(im, x, y, w, h, fill, border, inner, seed=0):
    im.noise_fill(x, y, w, h, fill, 0.03, seed)
    im.frame(x, y, w, h, BLACK)
    im.frame(x + 1, y + 1, w - 2, h - 2, border)
    im.frame(x + 2, y + 2, w - 4, h - 4, inner)


# ---------------- slots & hotbar ----------------
im = Img(20, 20); ornate_box(im, 0, 0, 20, 20, DARK2, GOLDD, DARK)
for (x, y) in [(3, 3), (16, 3), (3, 16), (16, 16)]: im.px(x, y, GOLDD)
im.save('UI_Slot')
im = Img(24, 24)
im.frame(0, 0, 24, 24, BLACK); im.frame(1, 1, 22, 22, GOLD); im.frame(2, 2, 20, 20, GOLDL); im.frame(3, 3, 18, 18, GOLDD)
koshkar_corner(im, 1, 1, WHITE); koshkar_corner(im, 22, 1, WHITE, True); koshkar_corner(im, 1, 22, WHITE, False, True); koshkar_corner(im, 22, 22, WHITE, True, True)
im.save('UI_SlotSelected')
im = Img(182, 22)
im.noise_fill(0, 0, 182, 22, DARK, 0.03, 1); im.frame(0, 0, 182, 22, BLACK); im.frame(1, 1, 180, 20, GOLDD)
for i in range(9):
    sx = 1 + i * 20
    im.frame(sx + 1, 2, 19, 18, DARK2)
    if i > 0: im.rect(sx, 2, 1, 18, GOLDD)
for x in range(4, 178, 6): im.px(x, 0, RED); im.px(x + 1, 0, GOLD); im.px(x, 21, RED); im.px(x + 1, 21, GOLD)
im.save('UI_Hotbar')

# ---------------- stat icons 9x9 ----------------
HEART = ["..##.##..", ".#######.", "#########", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...."]
def icon(name, shape, fill, outline, shine=None, half=False, empty=False):
    im = Img(9, 9)
    for y, row in enumerate(shape):
        for x, ch in enumerate(row):
            if ch != '#': continue
            edge = any(not (0 <= x + dx < 9 and 0 <= y + dy < 9) or shape[y + dy][x + dx] != '#' for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge: im.px(x, y, outline)
            elif empty or (half and x > 4): im.px(x, y, (0.20, 0.16, 0.14))
            else: im.px(x, y, fill)
    if shine and not empty: im.px(2, 2, shine); im.px(3, 2, shine) if not half or True else None
    im.save(name)
for suffix, kw in (('Full', {}), ('Half', {'half': True}), ('Empty', {'empty': True})):
    icon('UI_Heart' + suffix, HEART, (0.86, 0.12, 0.12), BLACK, (1.0, 0.75, 0.75), **kw)
BAURSAK = [".........", "..#####..", ".#######.", "#########", "#########", "#########", ".#######.", "..#####..", "........."]
for suffix, kw in (('Full', {}), ('Half', {'half': True}), ('Empty', {'empty': True})):
    icon('UI_Hunger' + suffix, BAURSAK, (0.85, 0.58, 0.25), (0.35, 0.18, 0.06), (1.0, 0.85, 0.55), **kw)
SHIELD = ["#########", "#########", "#########", "#########", ".#######.", ".#######.", "..#####..", "...###...", "....#...."]
for suffix, kw in (('Full', {}), ('Half', {'half': True}), ('Empty', {'empty': True})):
    icon('UI_Armor' + suffix, SHIELD, (0.75, 0.75, 0.80), BLACK, GOLD, **kw)
BUBBLE = ["...###...", ".##...##.", ".#.....#.", "#.......#", "#.......#", "#.......#", ".#.....#.", ".##...##.", "...###..."]
im = Img(9, 9)
for y, row in enumerate(BUBBLE):
    for x, ch in enumerate(row):
        if ch == '#': im.px(x, y, (0.55, 0.80, 1.0))
im.px(3, 3, WHITE); im.save('UI_Bubble')

# ---------------- xp bar ----------------
im = Img(182, 5); im.rect(0, 0, 182, 5, BLACK); im.rect(1, 1, 180, 3, (0.10, 0.18, 0.20))
for x in range(1, 181, 20): im.px(x, 1, GOLDD); im.px(x, 3, GOLDD)
im.save('UI_XpBarBg')
im = Img(182, 5)
for x in range(1, 181):
    im.px(x, 1, (0.55, 0.95, 0.90)); im.px(x, 2, TURQ); im.px(x, 3, TURQD)
im.save('UI_XpBarFill')

# ---------------- crosshair ----------------
im = Img(9, 9)
for i in range(9):
    if i != 4: im.px(i, 4, WHITE); im.px(4, i, WHITE)
im.px(4, 4, GOLD); im.save('UI_Crosshair')

# ---------------- buttons (9-slice: border 6) ----------------
def button(name, fill, border, text_glow):
    im = Img(64, 20)
    im.noise_fill(0, 0, 64, 20, fill, 0.03, 3)
    im.frame(0, 0, 64, 20, BLACK); im.frame(1, 1, 62, 18, border)
    im.rect(2, 2, 60, 1, text_glow); im.rect(2, 17, 60, 1, (fill[0] * 0.7, fill[1] * 0.7, fill[2] * 0.7))
    for (x, flip) in ((3, False), (60, True)):
        for (dx, dy) in [(0, 0), (1, -1), (1, 1), (2, -2), (2, 2), (0, -3), (0, 3)]:
            im.px(x + (-dx if flip else dx), 9 + dy, border)
    im.save(name)
button('UI_Button', RED, GOLD, (0.85, 0.30, 0.26))
button('UI_ButtonHover', (0.82, 0.22, 0.16), GOLDL, (1.0, 0.55, 0.45))
button('UI_ButtonDisabled', (0.35, 0.30, 0.28), (0.45, 0.42, 0.40), (0.42, 0.38, 0.36))
button('UI_ButtonBlue', BLUE, GOLD, (0.35, 0.50, 0.85))

# ---------------- panels (9-slice: border 12) ----------------
def panel(name, fill, b1, b2, ornament):
    im = Img(48, 48)
    im.noise_fill(0, 0, 48, 48, fill, 0.025, 5)
    im.frame(0, 0, 48, 48, BLACK); im.frame(1, 1, 46, 46, b1); im.frame(2, 2, 44, 44, b1)
    im.frame(3, 3, 42, 42, b2); im.frame(8, 8, 32, 32, b1)
    for i in range(5, 43, 4):
        im.px(i, 5, ornament); im.px(i + 1, 6, ornament); im.px(i, 42, ornament); im.px(i + 1, 41, ornament)
        im.px(5, i, ornament); im.px(6, i + 1, ornament); im.px(42, i, ornament); im.px(41, i + 1, ornament)
    for (x, y, fx, fy) in [(4, 4, False, False), (43, 4, True, False), (4, 43, False, True), (43, 43, True, True)]:
        koshkar_corner(im, x, y, GOLD, fx, fy)
    im.save(name)
panel('UI_Panel', FELT, RED, BLUE, RED)
panel('UI_PanelDark', DARK, GOLDD, REDD, GOLDD)

# ---------------- slider & checkbox ----------------
im = Img(32, 8); im.rect(0, 0, 32, 8, BLACK); im.noise_fill(1, 1, 30, 6, DARK2, 0.03, 7); im.rect(1, 3, 30, 2, GOLDD); im.save('UI_SliderBg')
im = Img(8, 14); im.rect(0, 0, 8, 14, BLACK); im.noise_fill(1, 1, 6, 12, RED, 0.03, 8); im.frame(1, 1, 6, 12, GOLD); im.save('UI_SliderKnob')
for on in (False, True):
    im = Img(12, 12); im.rect(0, 0, 12, 12, BLACK); im.noise_fill(1, 1, 10, 10, DARK2, 0.03, 9); im.frame(1, 1, 10, 10, GOLDD)
    if on:
        for (x, y) in [(3, 6), (4, 7), (5, 8), (6, 7), (7, 6), (8, 5), (9, 4)]: im.px(x, y, GOLDL); im.px(x, y - 1, GOLD)
    im.save('UI_Check' + ('On' if on else 'Off'))

# ---------------- crafting arrow & tab ----------------
im = Img(24, 17)
for y in range(17):
    for x in range(24):
        inside = (x < 14 and 5 <= y <= 11) or (x >= 14 and abs(y - 8) <= (23 - x) * 0.8)
        if inside: im.px(x, y, GOLDD if (y in (5, 11) and x < 14) else GOLD)
im.save('UI_CraftArrow')

# ---------------- logo: "KAZAKH MINE" with ram horns, subtitle "ДАЛА" ----------------
F = {  # 5x7 pixel glyphs
    'K': ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    'A': [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    'Z': ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    'H': ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    'M': ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    'I': ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
    'N': ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    'E': ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    'Д': [".###.", ".#.#.", ".#.#.", ".#.#.", ".#.#.", "#####", "#...#"],
    'Л': ["..###", ".#..#", ".#..#", ".#..#", ".#..#", "#...#", "#...#"],
    ' ': [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}
F['А'] = F['A']   # Cyrillic А
def text(im, s, x0, y0, scale, fill, shade, outline):
    pts = set()
    for i, ch in enumerate(s):
        g = F[ch]
        for y, row in enumerate(g):
            for x, c in enumerate(row):
                if c == '#':
                    for a in range(scale):
                        for b in range(scale): pts.add((x0 + (i * 6 + x) * scale + a, y0 + y * scale + b))
    for (x, y) in pts:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1, 2):
                if (x + dx, y + dy) not in pts: im.px(x + dx, y + dy, outline)
    for (x, y) in pts:
        rel = (y - y0) / (7 * scale)
        im.px(x, y, GOLDL if rel < 0.3 else (fill if rel < 0.7 else shade))
im = Img(256, 64)
text(im, "KAZAKH MINE", 31, 8, 3, GOLD, GOLDD, BLACK)
text(im, "ДАЛА", 104, 40, 2, (0.95, 0.35, 0.28), REDD, BLACK)
# ram horns flanking the title
for side in (-1, 1):
    cx = 128 + side * 118
    for t in range(40):
        a = t / 39 * math.pi * 1.6
        r = 9 - t * 0.17
        x = int(cx + side * (r * math.cos(a) - 4)); y = int(18 + r * math.sin(a))
        im.px(x, y, GOLD); im.px(x, y + 1, GOLDD)
im.save('UI_Logo')
UI_DONE = True
