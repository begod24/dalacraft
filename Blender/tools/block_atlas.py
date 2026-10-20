"""Kazakh Mine — block texture atlases: pages of 8x8 tiles, 16px each, stacked into one Texture2DArray in Unity.
Global tile index = page * 64 + slot (BlockInfo tile constants). Page 0 = T_BlockAtlas (nature, crafted, Kazakh),
page 1 = T_BlockAtlas_Trees (bark, leaves, saplings), page 2 = T_BlockAtlas_Build (glass, ladder, doors, trapdoors). Tile order = BlockInfo tile indices in Unity.
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
         'CraftTop', 'CraftSide', 'FurnaceFront', 'FurnaceFrontLit', 'SandstoneTop']
PAGES = {
    1: ['TurangaSide', 'TurangaTop', 'LeavesTugai', 'LeavesSaxaul',
        'SaplingBroad', 'SaplingBirch', 'SaplingSpruce', 'SaplingPine', 'SaplingApple', 'SaplingSaxaul', 'SaplingTugai', 'SaplingBlossom',
        'MushroomRed', 'MushroomBrown', 'GlowShroom', 'CrystalTurquoise', 'CrystalAmethyst', 'CrystalRuby', 'DripBase', 'DripMid', 'DripTip', 'LilyPad'],
    2: ['Glass', 'Ladder', 'DoorBottom', 'DoorTop', 'Trapdoor'],
}
T = {n: i for i, n in enumerate(NAMES)}
for pg, names in PAGES.items():
    for i, n in enumerate(names): T[n] = pg * 64 + i


class PageCanvas(Canvas):
    """8x8 page; accepts global tile indices (page * 64 + slot)."""
    def px(self, t, x, y, col, a=1.0): Canvas.px(self, t % 64, x, y, col, a)
c = PageCanvas(tiles=8, ts=16)
page0 = c
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
t = T['Felt']; c.fill(t, felt, 0.025); speck(t, mul(felt, 0.93), 12, 1)      # plain felt, Minecraft-calm
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
leaves(T['LeavesBroad'], (0.28, 0.50, 0.20), 31, 0.08); leaves(T['LeavesNeedle'], (0.19, 0.40, 0.22), 32, 0.05); leaves(T['LeavesBirch'], (0.42, 0.65, 0.25), 33, 0.09)

# ---- crafted / Kazakh style ----
t = T['Planks']; c.fill(t, (0.62, 0.44, 0.24), 0.03)
for y in (0, 4, 8, 12):
    for x in range(16): c.px(t, x, y, (0.42, 0.28, 0.14))
for y in range(16): c.px(t, ((0 if (y // 4) % 2 else 8) + 5) % 16, y, (0.48, 0.32, 0.16))
for x in range(2, 14, 3): c.px(t, x, 6, (0.50, 0.34, 0.17)); c.px(t, x + 1, 6, (0.50, 0.34, 0.17))
t = T['BirchSide']; c.fill(t, (0.93, 0.92, 0.87), 0.02)
for i in range(9): c.rect(t, int((hsh(i, 1, t) + 0.5) * 13), int((hsh(i, 2, t) + 0.5) * 15), 3, 1, (0.12, 0.11, 0.10))
t = T['BirchTop']; c.fill(t, (0.86, 0.76, 0.56), 0.03); border(t, (0.93, 0.92, 0.87)); rings(t, (0.72, 0.60, 0.40))
t = T['FeltRed']; c.fill(t, RED, 0.025); speck(t, mul(RED, 0.88), 12, 2)
t = T['FeltBlue']; c.fill(t, BLUE, 0.025); speck(t, mul(BLUE, 0.88), 12, 3)
# tekemet: red felt with one simple gold diamond; shyrdak: navy with one red stepped cross
t = T['Tekemet']; c.fill(t, (0.74, 0.20, 0.15), 0.025); c.diamond(t, 8, 8, 4, mul(GOLD, 0.9)); c.diamond(t, 8, 8, 2, mul(GOLD, 0.9))
t = T['Shyrdak']; c.fill(t, DARKB, 0.025)
for k in range(-4, 5): c.px(t, 8 + k, 8, RED); c.px(t, 8, 8 + k, RED)
for (dx, dy) in [(-4, 1), (-4, -1), (4, 1), (4, -1), (1, 4), (-1, 4), (1, -4), (-1, -4)]: c.px(t, 8 + dx, 8 + dy, RED)
t = T['Adobe']; c.fill(t, (0.80, 0.66, 0.46), 0.04)
for y in (0, 5, 10, 15):
    for x in range(16): c.px(t, x, y, (0.62, 0.50, 0.34))
for y in range(16):
    off = 0 if (y // 5) % 2 else 4
    c.px(t, off % 16, y, (0.62, 0.50, 0.34)); c.px(t, (off + 8) % 16, y, (0.62, 0.50, 0.34))
t = T['TileTurquoise']; c.fill(t, TURQ, 0.03)          # glazed tile: colour + grout line + a small light dot
for i in range(16): c.px(t, i, 0, mul(TURQ, 0.75)); c.px(t, 0, i, mul(TURQ, 0.75))
c.rect(t, 7, 7, 2, 2, (0.75, 0.92, 0.92))
t = T['TileMajolica']; c.fill(t, (0.17, 0.32, 0.70), 0.03)
for i in range(16): c.px(t, i, 0, (0.12, 0.22, 0.50)); c.px(t, 0, i, (0.12, 0.22, 0.50))
c.diamond(t, 8, 8, 2, TURQ)
# gold block: bright with a bevel, like Minecraft's
t = T['GoldOrnament']; c.fill(t, GOLD, 0.03)
for i in range(16): c.px(t, i, 15, (1.0, 0.92, 0.55)); c.px(t, 0, i, (1.0, 0.92, 0.55)); c.px(t, i, 0, (0.70, 0.50, 0.12)); c.px(t, 15, i, (0.70, 0.50, 0.12))
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
# sandstone like Minecraft's: smooth top, sides with a light cap band, sandy middle and a darker base band
t = T['Sandstone']; c.fill(t, (0.86, 0.77, 0.54), 0.025); speck(t, (0.80, 0.70, 0.48), 12, 7)
for x in range(16):
    for y in range(12, 16): c.px(t, x, y, (0.91, 0.83, 0.61) if y > 12 else (0.76, 0.66, 0.44))
    edge = 3 + (1 if (x // 2) % 3 == 0 else 0)
    for y in range(0, edge): c.px(t, x, y, (0.80, 0.70, 0.48) if y < edge - 1 else (0.74, 0.63, 0.42))
t = T['SandstoneTop']; c.fill(t, (0.90, 0.82, 0.60), 0.02); speck(t, (0.84, 0.75, 0.52), 8, 8)
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
leaves(T['LeavesAutumn'], (0.88, 0.50, 0.12), 41, 0.09)
leaves(T['LeavesBlossom'], (0.96, 0.66, 0.74), 42, 0.09)
t = T['LeavesApple']; leaves(t, (0.30, 0.54, 0.20), 43, 0.07)
for i in range(5): c.px(t, int((hsh(i, 1, t) + 0.5) * 13) + 1, int((hsh(i, 2, t) + 0.5) * 13) + 1, (0.85, 0.12, 0.10))
t = T['LeavesSnow']; leaves(t, (0.14, 0.32, 0.20), 44, 0.05)
# snow lies in clumps on the needles: solid at the top edge, thinning downwards
for y in range(16):
    for x in range(16):
        lim = 0.65 - (y - 6) * 0.11              # y = 15 is the top row of the tile
        if hsh(x // 2, y // 2, 45) + 0.5 > lim: c.px(t, x, y, (0.92 + hsh(x, y, 46) * 0.06, 0.95, 1.0), 1.0)
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

K['save_png']('T_BlockAtlas', page0.a)

# ================= page 1: trees =================
c = PageCanvas(tiles=8, ts=16); c.a[:] = 0.0
# ---- trees from blocks: tugai poplar (turanga) bark, silvery tugai and sparse saxaul leaves ----
t = T['TurangaSide']; c.fill(t, (0.55, 0.50, 0.42), 0.04)
for x0 in (1, 5, 9, 13):
    for y in range(16):
        x = x0 + (1 if math.sin(y * 0.9 + x0) > 0.3 else 0)
        c.px(t, x, y, (0.36, 0.32, 0.26)); 
        if hsh(x0, y, 3) > 0.25: c.px(t, x + 1, y, (0.42, 0.37, 0.30))
speck(t, (0.66, 0.62, 0.54), 8, 9)
t = T['TurangaTop']; c.fill(t, (0.80, 0.70, 0.52), 0.03); rings(t, (0.64, 0.54, 0.38)); border(t, (0.42, 0.37, 0.30))
t = T['LeavesTugai']; leaves(t, (0.42, 0.56, 0.36), 46, 0.09)
for i in range(14): c.px(t, int((hsh(i, 1, t) + 0.5) * 16) % 16, int((hsh(i, 2, t) + 0.5) * 16) % 16, (0.72, 0.80, 0.68))
t = T['LeavesSaxaul']; leaves(t, (0.52, 0.60, 0.38), 47, 0.3)

# ---- saplings (crossed sprites) ----
def sapling(name, stem, leaf, shape, seed, fruit=None):
    t = T[name]
    for y in range(16):
        for x in range(16): c.px(t, x, y, (0, 0, 0), 0.0)
    for y in range(0, 7): c.px(t, 7 + (1 if y > 4 and shape == 'bent' else 0), y, stem)
    if shape in ('round', 'bent'):
        for (cx, cy, r) in [(7, 9, 4), (4, 7, 2), (11, 8, 2)]:
            for x in range(cx - r, cx + r + 1):
                for y in range(cy - r, cy + r + 1):
                    if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 1 and hsh(x, y, seed) > -0.38:
                        c.px(t, x, y, mul(leaf, 1.15 if hsh(x, y, seed + 1) > 0.2 else 0.85))
    elif shape == 'cone':
        for y in range(4, 15):
            w = (15 - y) // 2
            for x in range(7 - w, 8 + w):
                if hsh(x, y, seed) > -0.42: c.px(t, x, y, mul(leaf, 1.1 if (x + y) % 3 else 0.8))
        c.px(t, 7, 15, leaf)
    elif shape == 'sparse':
        for (x0, y0, dx) in [(7, 6, -1), (7, 8, 1), (7, 10, -1), (8, 12, 1)]:
            for i in range(4): c.px(t, x0 + dx * i, y0 + i // 2, stem)
            for i in range(3): c.px(t, x0 + dx * (3 + i), y0 + 2 + (i % 2), leaf)
    if fruit:
        for (x, y) in [(5, 9), (10, 10), (8, 12)]: c.px(t, x, y, fruit)
sapling('SaplingBroad', (0.42, 0.29, 0.16), (0.30, 0.52, 0.20), 'round', 51)
sapling('SaplingBirch', (0.92, 0.90, 0.85), (0.44, 0.66, 0.26), 'round', 52)
sapling('SaplingSpruce', (0.30, 0.20, 0.13), (0.13, 0.32, 0.18), 'cone', 53)
sapling('SaplingPine', (0.62, 0.36, 0.20), (0.18, 0.38, 0.20), 'cone', 54)
sapling('SaplingApple', (0.42, 0.29, 0.16), (0.30, 0.54, 0.20), 'round', 55, fruit=(0.85, 0.12, 0.10))
sapling('SaplingSaxaul', (0.58, 0.52, 0.44), (0.52, 0.62, 0.38), 'sparse', 56)
sapling('SaplingTugai', (0.55, 0.50, 0.42), (0.45, 0.58, 0.40), 'bent', 57)
sapling('SaplingBlossom', (0.42, 0.29, 0.16), (0.96, 0.66, 0.74), 'round', 58)

# ---- small plants & cave growths drawn as crossed sprites (Minecraft mushrooms, amethyst, pointed dripstone) ----
def clear(t):
    for y in range(16):
        for x in range(16): c.px(t, x, y, (0, 0, 0), 0.0)
def mushroom(name, cap, capD, dots, stem=(0.90, 0.86, 0.76), tall=False):
    t = T[name]; clear(t)
    for (cx, h, r) in ([(5, 6, 3), (11, 9, 3)] if not tall else [(7, 10, 4)]):
        for y in range(0, h): c.px(t, cx, y, stem); c.px(t, cx + 1, y, mul(stem, 0.85))
        for y in range(h, h + 3):
            w = r if y < h + 2 else r - 1
            for x in range(cx - w + 1, cx + w + 1): c.px(t, x, y, cap if y > h else capD)
        if dots:
            for (dx, dy) in [(-1, 1), (1, 2), (2, 1)]: c.px(t, cx + dx, h + dy, dots)
mushroom('MushroomRed', (0.86, 0.12, 0.10), (0.62, 0.08, 0.06), (0.97, 0.95, 0.90))
mushroom('MushroomBrown', (0.55, 0.34, 0.18), (0.40, 0.24, 0.12), None)
mushroom('GlowShroom', (0.35, 0.85, 1.0), (0.20, 0.55, 0.85), (0.85, 1.0, 1.0), stem=(0.75, 0.92, 0.95))
def crystal(name, col):
    t = T[name]; clear(t)
    for (cx, h, w) in [(7, 14, 2), (3, 8, 1), (12, 10, 1), (10, 6, 1)]:
        for y in range(h):
            ww = w if y < h - 2 else max(0, w - 1)
            for x in range(cx - ww, cx + ww + 1):
                c.px(t, x, y, mul(col, 1.35 if x == cx - ww else (0.75 if x == cx + ww else 1.0)))
        c.px(t, cx, h, mul(col, 1.5))
crystal('CrystalTurquoise', (0.25, 0.78, 0.80)); crystal('CrystalAmethyst', (0.62, 0.38, 0.88)); crystal('CrystalRuby', (0.86, 0.18, 0.22))
DRIP, DRIPD = (0.58, 0.48, 0.40), (0.42, 0.34, 0.28)
for name, w0, w1 in (('DripBase', 5, 4), ('DripMid', 4, 3), ('DripTip', 3, 0)):
    t = T[name]; clear(t)
    for y in range(16):          # y = 0 is the narrow end (drawn pointing down; flipped for stalagmites)
        w = w1 + (w0 - w1) * y / 15
        for x in range(16):
            if abs(x - 7.5) <= w: c.px(t, x, y, DRIPD if (x + y) % 5 == 0 or x > 7.5 + w - 1 else DRIP)
t = T['LilyPad']; clear(t)
for y in range(16):
    for x in range(16):
        d = math.hypot(x - 7.5, y - 7.5)
        if d < 7.2 and not (x > 7 and abs(y - 7.5) < 1.2):          # round leaf with a notch
            c.px(t, x, y, (0.20, 0.50, 0.18) if d < 6 else (0.14, 0.38, 0.12))
for i in range(6): c.px(t, 7 - i, 7 + i // 2, (0.28, 0.62, 0.24))
K['save_png']('T_BlockAtlas_Trees', c.a)

# ================= page 2: building =================
c = PageCanvas(tiles=8, ts=16); c.a[:] = 0.0
# ---- glass: light frame + a few glints, see-through (alpha cut-out) ----
t = T['Glass']
for y in range(16):
    for x in range(16): c.px(t, x, y, (0, 0, 0), 0.0)
fr = (0.86, 0.95, 0.98)
for i in range(16): c.px(t, i, 0, fr); c.px(t, i, 15, fr); c.px(t, 0, i, fr); c.px(t, 15, i, fr)
for i in range(3): c.px(t, 3 + i, 11 - i, (0.95, 1.0, 1.0)); c.px(t, 9 + i, 6 - i, (0.95, 1.0, 1.0))
c.px(t, 12, 12, (0.95, 1.0, 1.0))

# ---- ladder: two rails, four rungs, transparent between ----
t = T['Ladder']; wl, wd = (0.62, 0.44, 0.24), (0.40, 0.27, 0.14)
for y in range(16):
    for x in range(16): c.px(t, x, y, (0, 0, 0), 0.0)
for y in range(16):
    for x in (2, 3, 12, 13): c.px(t, x, y, wl if x in (2, 12) else wd)
for y0 in (1, 5, 9, 13):
    for x in range(4, 12): c.px(t, x, y0, wl); c.px(t, x, y0 + 1, wd)

# ---- carved Kazakh door (есік): red-brown planks, gold koshkar panels; the top half has a lattice window ----
DOOR, DOORD, ORN = (0.62, 0.26, 0.14), (0.44, 0.18, 0.09), (0.90, 0.70, 0.24)
for name in ('DoorBottom', 'DoorTop'):
    t = T[name]; c.fill(t, DOOR, 0.025)
    for i in range(16): c.px(t, 0, i, DOORD); c.px(t, 15, i, DOORD)
    for i in range(16): c.px(t, i, 0 if name == 'DoorBottom' else 15, DOORD)
    if name == 'DoorBottom':
        c.diamond(t, 8, 8, 3, ORN); c.px(t, 12, 8, (0.25, 0.25, 0.27))
    else:
        for x in range(5, 11):
            for y in range(6, 12): c.px(t, x, y, (0, 0, 0), 0.0)            # a small window
        for x in range(5, 11): c.px(t, x, 9, DOORD)
        for y in range(6, 12): c.px(t, 8, y, DOORD)
# trapdoor (люк): planks with a frame and a cross brace, small gaps
t = T['Trapdoor']; c.fill(t, (0.60, 0.42, 0.22), 0.03)
for i in range(16): c.px(t, i, 0, DOORD); c.px(t, i, 15, DOORD); c.px(t, 0, i, DOORD); c.px(t, 15, i, DOORD)
for i in range(1, 15): c.px(t, i, i, DOORD); c.px(t, i, 15 - i, DOORD)
for (x, y) in [(4, 7), (5, 7), (11, 8), (10, 8), (7, 4), (8, 11)]: c.px(t, x, y, (0, 0, 0), 0.0)

K['save_png']('T_BlockAtlas_Build', c.a)
# crack overlay strip for block-breaking progress: 10 stages, 16px each. Cracks branch out from a few seeds and
# grow with every stage (each stage contains the previous one), drawn dark and slightly see-through like Minecraft.
import numpy as np
crack = np.zeros((16, 160, 4), dtype=np.float32)
paths = []
for k in range(7):                       # crack polylines: start near the centre, random-walk outwards
    x, y = 7.5 + hsh(k, 1, 90) * 5, 7.5 + hsh(k, 2, 90) * 5
    ang = (k * 2.4) % 6.283
    pts = []
    for step in range(16):
        ang += hsh(k, step, 91) * 1.4
        x += math.cos(ang); y += math.sin(ang)
        pts.append((int(round(x)), int(round(y))))
        if step % 5 == 4:                # side branch
            bx, by, ba = x, y, ang + (1.3 if step % 2 else -1.3)
            for j in range(3):
                bx += math.cos(ba); by += math.sin(ba); pts.append((int(round(bx)), int(round(by))))
    paths.append(pts)
for s in range(10):
    n = int(2 + s * 1.7)                 # pixels per crack shown at this stage
    for k, pts in enumerate(paths):
        if k > 2 + s: continue
        for (x, y) in pts[:n]:
            if 0 <= x < 16 and 0 <= y < 16: crack[y, s * 16 + x] = (0.0, 0.0, 0.0, 0.78)
K['save_png']('T_Cracks', crack)

BLOCK_TILES = T
bpy.app.driver_namespace['km_tiles'] = T
