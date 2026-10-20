"""Kazakh Mine — block texture atlas (8x8 tiles, 16px). Tile order = BlockInfo tile indices in Unity.
Run inside Blender after km_tools.py:  exec(open(".../Blender/tools/block_atlas.py").read())
"""
import bpy, math
K = bpy.app.driver_namespace['km']; Canvas, mul, hsh = K['Canvas'], K['mul'], K['hsh']

NAMES = ['GrassTop', 'GrassSide', 'Dirt', 'Stone', 'Snow', 'Sand', 'Water', 'Felt', 'WoodSide', 'DryTop', 'DrySide', 'SnowSide', 'WoodTop',
         'Ice', 'Gravel', 'Clay', 'Takyr', 'RedSand', 'RedStone', 'OreCoal', 'OreIron', 'OreCopper', 'OreGold', 'LeavesBroad', 'LeavesNeedle', 'LeavesBirch',
         'Planks', 'BirchSide', 'BirchTop', 'FeltRed', 'FeltBlue', 'Tekemet', 'Shyrdak', 'Adobe', 'TileTurquoise', 'TileMajolica', 'GoldOrnament', 'Cobble',
         'StoneBrick', 'Sandstone', 'HaySide', 'HayTop', 'Thatch', 'WoolWhite', 'WoolBrown', 'MossStone', 'Bedrock', 'Basalt', 'Salt',
         # survival additions (49+)
         'SpruceSide', 'SpruceTop', 'PineSide', 'PineTop', 'SaxaulSide', 'SaxaulTop', 'LeavesAutumn', 'LeavesBlossom', 'LeavesApple', 'LeavesSnow',
         'CraftTop', 'CraftSide', 'FurnaceFront', 'FurnaceFrontLit']
T = {n: i for i, n in enumerate(NAMES)}
c = Canvas(tiles=8, ts=16)
grass = (0.42, 0.62, 0.24); dry = (0.74, 0.66, 0.36); dirt = (0.46, 0.32, 0.20); stone = (0.53, 0.53, 0.55); snow = (0.93, 0.95, 0.99)
sand = (0.87, 0.79, 0.56); water = (0.18, 0.42, 0.72); felt = (0.92, 0.88, 0.78); wood = (0.42, 0.29, 0.16)
RED = (0.72, 0.13, 0.11); GOLD = (0.90, 0.70, 0.22); BLUE = (0.16, 0.32, 0.62); TURQ = (0.16, 0.62, 0.66); WHITE = (0.96, 0.94, 0.88); DARKB = (0.08, 0.14, 0.32)


def speck(t, col, n, seed):
    for i in range(n):
        c.px(t, int((hsh(t, i, seed, 1) + 0.5) * 16) % 16, int((hsh(t, i, seed, 2) + 0.5) * 16) % 16, col)


def topgrass(t, base):
    c.fill(t, base, 0.06); speck(t, mul(base, 0.78), 30, 3); speck(t, mul(base, 1.15), 22, 4)


def side(t, top, body, depth=4):
    c.fill(t, body, 0.05); speck(t, mul(body, 0.75), 10, 5); speck(t, mul(body, 1.2), 6, 6)
    for x in range(16):
        d = depth + int((hsh(t, x, 9) + 0.5) * 3) - (1 if x % 5 == 0 else 0)
        for y in range(16 - d, 16):
            n = hsh(t, x, y) * 0.1; c.px(t, x, y, (top[0] + n, top[1] + n, top[2] + n))


def koshkar(t, cx, cy, col):
    """қошқар мүйіз — ram-horn motif"""
    for (dx, dy) in [(0, 0), (-1, 1), (1, 1), (-2, 2), (2, 2), (-3, 1), (3, 1), (-3, 0), (3, 0), (-2, -1), (2, -1), (0, -1), (0, -2), (-1, -3), (1, -3)]:
        c.px(t, cx + dx, cy + dy, col)


def border(t, col):
    for i in range(16): c.px(t, i, 0, col); c.px(t, i, 15, col); c.px(t, 0, i, col); c.px(t, 15, i, col)


def rings(t, col, radii=(2, 4, 6)):
    for r in radii:
        for a in range(64): c.px(t, int(round(7.5 + r * math.cos(a / 64 * 6.283))), int(round(7.5 + r * math.sin(a / 64 * 6.283))), col)


def ore(t, col, colL):
    c.fill(t, stone, 0.07)
    for i in range(6):
        cx = int((hsh(i, 1, t) + 0.5) * 13) + 1; cy = int((hsh(i, 2, t) + 0.5) * 13) + 1
        for (dx, dy) in [(0, 0), (1, 0), (0, 1), (1, 1)]:
            if hsh(cx + dx, cy + dy, t) > -0.3: c.px(t, cx + dx, cy + dy, col)
        c.px(t, cx, cy + 1, colL)


def leaves(t, base, seed, hole=0.18):
    for y in range(16):
        for x in range(16):
            h = hsh(x, y, seed)
            if h < -0.5 + hole: c.px(t, x, y, (0, 0, 0), 0.0)
            else: c.px(t, x, y, mul(base, 1.15 if h > 0.3 else (0.8 if h < -0.15 else 1.0)))


# ---- original tiles (indices unchanged) ----
topgrass(T['GrassTop'], grass); side(T['GrassSide'], grass, dirt)
c.fill(T['Dirt'], dirt, 0.05); speck(T['Dirt'], mul(dirt, 0.7), 14, 1); speck(T['Dirt'], (0.55, 0.5, 0.45), 6, 2)
t = T['Stone']; c.fill(t, stone, 0.07)
for i in range(5):
    cx = int((hsh(i, 1, t) + 0.5) * 16); cy = int((hsh(i, 2, t) + 0.5) * 16)
    for dx in range(-1, 2): c.px(t, cx + dx, cy, mul(stone, 0.82)); c.px(t, cx + dx, cy - 1, mul(stone, 0.82))
for (x0, y0, dx, dy, n) in [(2, 12, 1, -1, 5), (9, 4, 1, 1, 5), (11, 13, -1, -1, 4)]:
    for i in range(n): c.px(t, x0 + i * dx, y0 + i * dy, mul(stone, 0.62))
c.fill(T['Snow'], snow, 0.025); speck(T['Snow'], (0.82, 0.88, 0.97), 10, 2)
t = T['Sand']; c.fill(t, sand, 0.04); speck(t, mul(sand, 0.85), 18, 3); speck(t, (0.95, 0.9, 0.72), 10, 4)
for y in (4, 11):
    for x in range(16):
        if (x + y) % 5: c.px(t, x, y + (1 if x % 7 < 3 else 0), mul(sand, 0.92))
t = T['Water']; c.fill(t, water, 0.025)
for y in (2, 7, 12):
    for x in range(16):
        if (x // 3 + y) % 3 == 0: c.px(t, x, y, (0.42, 0.65, 0.90))
t = T['Felt']; c.fill(t, felt, 0.03); border(t, RED); koshkar(t, 8, 8, RED); c.px(t, 8, 8, GOLD)
for i in range(16): c.px(t, i, 1, GOLD); c.px(t, i, 14, GOLD)
t = T['WoodSide']; c.fill(t, wood, 0.04)
for x in range(0, 16, 3):
    for y in range(16):
        if hsh(x, y, t) > -0.3: c.px(t, x, y, mul(wood, 0.72))
topgrass(T['DryTop'], dry); side(T['DrySide'], dry, dirt, 3); side(T['SnowSide'], snow, dirt, 5)
t = T['WoodTop']; c.fill(t, (0.70, 0.52, 0.30), 0.03); rings(t, (0.55, 0.38, 0.20)); border(t, wood)

# ---- natural ----
t = T['Ice']; c.fill(t, (0.62, 0.83, 0.96), 0.03)
for i in range(5): c.px(t, 3 + i * 2, 12 - i, (0.90, 0.97, 1.0)); c.px(t, 4 + i * 2, 12 - i, (0.90, 0.97, 1.0))
for (x0, y0) in [(2, 3), (10, 8)]:
    for i in range(4): c.px(t, x0 + i, y0 + (i % 2), (0.45, 0.70, 0.90))
t = T['Gravel']; c.fill(t, (0.50, 0.48, 0.46), 0.05)
for i in range(18):
    c.rect(t, int((hsh(i, 1, t) + 0.5) * 15), int((hsh(i, 2, t) + 0.5) * 15), 2, 2, [(0.62, 0.60, 0.58), (0.38, 0.36, 0.35), (0.55, 0.50, 0.45)][i % 3])
t = T['Clay']; c.fill(t, (0.62, 0.58, 0.56), 0.03)
for y in (5, 11):
    for x in range(16): c.px(t, x, y, (0.56, 0.52, 0.50))
t = T['Takyr']; c.fill(t, (0.80, 0.68, 0.52), 0.04)
for x in range(16):
    c.px(t, x, int(5 + 2 * math.sin(x * 0.8)), (0.50, 0.40, 0.30)); c.px(t, x, int(12 + 1.5 * math.sin(x * 1.3 + 1)), (0.50, 0.40, 0.30))
for y in range(16): c.px(t, int(7 + 2 * math.sin(y * 0.7)), y, (0.50, 0.40, 0.30))
c.fill(T['RedSand'], (0.78, 0.45, 0.25), 0.04); speck(T['RedSand'], (0.65, 0.35, 0.18), 16, 5)
t = T['RedStone']
for y in range(16):
    band = [(0.70, 0.30, 0.18), (0.82, 0.45, 0.28), (0.86, 0.56, 0.30), (0.58, 0.24, 0.15)][(y // 4) % 4]
    for x in range(16):
        n = hsh(x, y, t) * 0.06; c.px(t, x, y, (band[0] + n, band[1] + n, band[2] + n))
ore(T['OreCoal'], (0.10, 0.10, 0.11), (0.25, 0.25, 0.27)); ore(T['OreIron'], (0.78, 0.62, 0.50), (0.90, 0.78, 0.66))
ore(T['OreCopper'], (0.80, 0.45, 0.25), (0.35, 0.70, 0.55)); ore(T['OreGold'], (0.98, 0.82, 0.25), (1.0, 0.95, 0.60))
leaves(T['LeavesBroad'], (0.28, 0.50, 0.20), 31); leaves(T['LeavesNeedle'], (0.12, 0.30, 0.17), 32, 0.1); leaves(T['LeavesBirch'], (0.42, 0.65, 0.25), 33, 0.22)

# ---- crafted / Kazakh style ----
t = T['Planks']; c.fill(t, (0.62, 0.44, 0.24), 0.03)
for y in (0, 4, 8, 12):
    for x in range(16): c.px(t, x, y, (0.42, 0.28, 0.14))
for y in range(16): c.px(t, ((0 if (y // 4) % 2 else 8) + 5) % 16, y, (0.48, 0.32, 0.16))
for x in range(2, 14, 3): c.px(t, x, 6, (0.50, 0.34, 0.17)); c.px(t, x + 1, 6, (0.50, 0.34, 0.17))
t = T['BirchSide']; c.fill(t, (0.93, 0.92, 0.87), 0.02)
for i in range(9): c.rect(t, int((hsh(i, 1, t) + 0.5) * 13), int((hsh(i, 2, t) + 0.5) * 15), 3, 1, (0.12, 0.11, 0.10))
t = T['BirchTop']; c.fill(t, (0.86, 0.76, 0.56), 0.03); border(t, (0.93, 0.92, 0.87)); rings(t, (0.72, 0.60, 0.40))
t = T['FeltRed']; c.fill(t, RED, 0.03); border(t, GOLD); koshkar(t, 8, 8, GOLD); c.px(t, 8, 8, WHITE)
t = T['FeltBlue']; c.fill(t, BLUE, 0.03); border(t, WHITE); koshkar(t, 8, 8, WHITE); c.px(t, 8, 8, RED)
t = T['Tekemet']; c.fill(t, (0.80, 0.20, 0.15), 0.03)
for x in range(16):
    for y in range(16):
        if abs(abs(x - 7.5) + abs(y - 7.5) - 6) < 0.6: c.px(t, x, y, GOLD)
koshkar(t, 4, 8, WHITE); koshkar(t, 12, 8, WHITE); border(t, BLUE)
t = T['Shyrdak']
for x in range(16):
    for y in range(16):
        u, v = min(x, 15 - x), min(y, 15 - y)
        on = (u + v) % 6 in (0, 1) or (u in (2, 3) and v > 3) or (v in (2, 3) and u > 3)
        c.px(t, x, y, RED if on else DARKB)
border(t, WHITE)
t = T['Adobe']; c.fill(t, (0.80, 0.66, 0.46), 0.04)
for y in (0, 5, 10, 15):
    for x in range(16): c.px(t, x, y, (0.62, 0.50, 0.34))
for y in range(16):
    off = 0 if (y // 5) % 2 else 4
    c.px(t, off % 16, y, (0.62, 0.50, 0.34)); c.px(t, (off + 8) % 16, y, (0.62, 0.50, 0.34))
t = T['TileTurquoise']; c.fill(t, TURQ, 0.04)   # glazed tile, Khoja Ahmed Yasawi style
for i in range(16): c.px(t, i, 0, (0.10, 0.40, 0.45)); c.px(t, 0, i, (0.10, 0.40, 0.45))
c.diamond(t, 8, 8, 4, WHITE); c.diamond(t, 8, 8, 2, (0.15, 0.30, 0.70)); c.px(t, 8, 8, GOLD)
t = T['TileMajolica']; c.fill(t, (0.15, 0.30, 0.70), 0.04)
for x in range(16):
    for y in range(16):
        d = math.hypot(x - 7.5, y - 7.5)
        if 3.5 < d < 4.6: c.px(t, x, y, TURQ)
        if d < 1.6: c.px(t, x, y, WHITE)
for (x, y) in [(1, 1), (14, 1), (1, 14), (14, 14)]: c.diamond(t, x, y, 1, TURQ)
t = T['GoldOrnament']; c.fill(t, GOLD, 0.05); border(t, (0.70, 0.50, 0.12)); koshkar(t, 8, 8, (0.70, 0.50, 0.12))
t = T['Cobble']; c.fill(t, (0.45, 0.45, 0.46), 0.03)
for i in range(9):
    x = int((hsh(i, 1, t) + 0.5) * 12) + 2; y = int((hsh(i, 2, t) + 0.5) * 12) + 2; r = 2 + int((hsh(i, 3, t) + 0.5) * 2)
    for dx in range(-r, r + 1):
        for dy in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r: c.px(t, x + dx, y + dy, mul(stone, 1.05 + 0.1 * hsh(i, dx, dy)))
t = T['StoneBrick']; c.fill(t, (0.58, 0.58, 0.60), 0.04)
for y in (0, 7, 8, 15):
    for x in range(16): c.px(t, x, y, (0.40, 0.40, 0.42))
for y in range(8): c.px(t, 0, y, (0.40, 0.40, 0.42)); c.px(t, 8, y + 8, (0.40, 0.40, 0.42))
c.diamond(t, 4, 11, 2, (0.45, 0.45, 0.48)); c.diamond(t, 12, 4, 2, (0.45, 0.45, 0.48))
t = T['Sandstone']; c.fill(t, (0.88, 0.78, 0.56), 0.03)
for y in (3, 8, 13):
    for x in range(16): c.px(t, x, y, (0.78, 0.66, 0.44))
t = T['HaySide']; c.fill(t, (0.86, 0.74, 0.32), 0.05)
for x in range(16):
    for y in range(16):
        if (x + y * 3) % 5 == 0: c.px(t, x, y, (0.72, 0.58, 0.20))
for x in range(16): c.px(t, x, 4, RED); c.px(t, x, 11, RED)
t = T['HayTop']; c.fill(t, (0.80, 0.68, 0.28), 0.06); rings(t, (0.68, 0.55, 0.20), (2, 5))
t = T['Thatch']; c.fill(t, (0.62, 0.55, 0.32), 0.05)
for x in range(16):
    for y in range(0, 16, 4): c.px(t, x, (y + x // 4) % 16, (0.45, 0.38, 0.20))
t = T['WoolWhite']; c.fill(t, WHITE, 0.04); speck(t, (0.85, 0.83, 0.78), 20, 1)
t = T['WoolBrown']; c.fill(t, (0.45, 0.32, 0.22), 0.04); speck(t, (0.35, 0.25, 0.17), 20, 2)
t = T['MossStone']; c.fill(t, stone, 0.06)
for x in range(16):
    for y in range(16):
        if hsh(x, y, t) > 0.1 + (y / 30): c.px(t, x, y, (0.32 + hsh(x, y, 1) * 0.06, 0.50, 0.20))
t = T['Bedrock']; c.fill(t, (0.20, 0.20, 0.21), 0.10); speck(t, (0.05, 0.05, 0.05), 30, 1)
t = T['Basalt']; c.fill(t, (0.25, 0.24, 0.26), 0.04)
for x in (0, 5, 10, 15):
    for y in range(16): c.px(t, x, y, (0.15, 0.15, 0.16))
t = T['Salt']; c.fill(t, (0.97, 0.96, 0.94), 0.02)
for i in range(6): c.px(t, int((hsh(i, 1, t) + 0.5) * 15), int((hsh(i, 2, t) + 0.5) * 15), (0.85, 0.90, 0.95))

# ---- survival additions: one bark per tree family, seasonal leaves, workbench, furnace ----
def bark(name, base, dark, top_base, ring, vertical=3, seed=0):
    t = T[name + 'Side']; c.fill(t, base, 0.04)
    for x in range(0, 16, vertical):
        for y in range(16):
            if hsh(x, y, t, seed) > -0.3: c.px(t, x, y, mul(dark, 1.0))
    for i in range(7): c.px(t, int((hsh(i, 5, t) + 0.5) * 15), int((hsh(i, 6, t) + 0.5) * 15), mul(base, 1.2))
    t = T[name + 'Top']; c.fill(t, top_base, 0.03); rings(t, ring); border(t, dark)

bark('Spruce', (0.30, 0.20, 0.13), (0.20, 0.13, 0.08), (0.60, 0.44, 0.27), (0.45, 0.30, 0.17), 3, 1)
bark('Pine', (0.62, 0.36, 0.20), (0.42, 0.22, 0.12), (0.78, 0.56, 0.33), (0.60, 0.40, 0.22), 4, 2)
bark('Saxaul', (0.58, 0.52, 0.44), (0.38, 0.33, 0.28), (0.72, 0.64, 0.50), (0.55, 0.47, 0.35), 2, 3)
leaves(T['LeavesAutumn'], (0.88, 0.50, 0.12), 41, 0.2)
leaves(T['LeavesBlossom'], (0.96, 0.66, 0.74), 42, 0.2)
t = T['LeavesApple']; leaves(t, (0.30, 0.54, 0.20), 43, 0.15)
for i in range(5): c.px(t, int((hsh(i, 1, t) + 0.5) * 13) + 1, int((hsh(i, 2, t) + 0.5) * 13) + 1, (0.85, 0.12, 0.10))
t = T['LeavesSnow']; leaves(t, (0.14, 0.32, 0.20), 44, 0.1)
for y in range(16):
    for x in range(16):
        if hsh(x, y, 45) > 0.08 and y < 11: c.px(t, x, y, (0.93, 0.96, 1.0), 1.0)
# workbench (Kazakh carved table): planks top with tool outlines, carved side
t = T['CraftTop']; c.fill(t, (0.62, 0.44, 0.24), 0.03)
for i in range(16): c.px(t, i, 0, (0.30, 0.20, 0.10)); c.px(t, i, 15, (0.30, 0.20, 0.10)); c.px(t, 0, i, (0.30, 0.20, 0.10)); c.px(t, 15, i, (0.30, 0.20, 0.10))
for i in range(1, 15): c.px(t, i, 5, (0.42, 0.28, 0.14)); c.px(t, i, 10, (0.42, 0.28, 0.14))
for i in range(4): c.px(t, 3 + i, 3, (0.7, 0.7, 0.72)); c.px(t, 9, 2 + i, (0.4, 0.4, 0.42)); c.px(t, 10 + i, 12, (0.55, 0.55, 0.58))
t = T['CraftSide']; c.fill(t, (0.52, 0.36, 0.20), 0.03)
for y in range(0, 4):
    for x in range(16): c.px(t, x, y, (0.30, 0.20, 0.10) if y == 0 else (0.66, 0.47, 0.26))
for x in range(16):
    for y in range(4, 16):
        if (x + y) % 8 == 0 or (x - y) % 8 == 0: c.px(t, x, y, (0.40, 0.26, 0.13))
koshkar(t, 8, 9, (0.90, 0.70, 0.22))
# furnace (clay-and-stone pech): cobble sides, mouth in front
def furnace_front(name, lit):
    t = T[name]; c.fill(t, (0.46, 0.46, 0.48), 0.04)
    for y in (0, 7, 8, 15):
        for x in range(16): c.px(t, x, y, (0.30, 0.30, 0.32))
    for x in range(4, 12):
        for y in range(5, 13): c.px(t, x, y, (0.07, 0.07, 0.08))
    for x in range(3, 13): c.px(t, x, 4, (0.25, 0.25, 0.27)); 
    if lit:
        for x in range(5, 11):
            for y in range(8, 13): c.px(t, x, y, (0.98, 0.55 + 0.25 * hsh(x, y, 9), 0.10))
        for x in range(6, 10): c.px(t, x, 8, (1.0, 0.9, 0.35))
furnace_front('FurnaceFront', False); furnace_front('FurnaceFrontLit', True)

K['save_png']('T_BlockAtlas', c.a)
# crack overlay strip for block-breaking progress: 10 stages, 16px each
import numpy as np
crack = np.zeros((16, 160, 4), dtype=np.float32)
for s in range(10):
    for i in range(int(6 + s * 7)):
        a = hsh(i, s, 77) + 0.5; b = hsh(i, s, 78) + 0.5
        cx0, cy0 = 8, 8
        ang = (i * 2.399) % 6.283; r = (i % 9) * 0.9 * (0.4 + s / 8)
        x = int(cx0 + math.cos(ang) * r); y = int(cy0 + math.sin(ang) * r)
        if 0 <= x < 16 and 0 <= y < 16:
            crack[y, s * 16 + x] = (0.05, 0.05, 0.05, 0.85)
K['save_png']('T_Cracks', crack)

BLOCK_TILES = T
