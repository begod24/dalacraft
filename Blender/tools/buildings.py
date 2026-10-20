"""Kazakh Mine — aul buildings & crafting stations (voxel props).
Run inside Blender after km_tools.py. Exports to Resources/Models/Buildings/.
"""
import bpy, math
K = bpy.app.driver_namespace['km']; hsh, vbox, line, voxel_mesh, get_coll = K['hsh'], K['vbox'], K['line'], K['voxel_mesh'], K['get_coll']

RED = (0.72, 0.13, 0.11); REDD = (0.50, 0.08, 0.07); GOLD = (0.90, 0.70, 0.22); BLUE = (0.16, 0.32, 0.62); TURQ = (0.18, 0.64, 0.68)
TURQL = (0.40, 0.82, 0.84); WHITE = (0.95, 0.93, 0.88); FELT = (0.93, 0.90, 0.81); FELTD = (0.80, 0.76, 0.66); GREYF = (0.62, 0.60, 0.56)
WOOD = (0.52, 0.33, 0.17); WOODD = (0.36, 0.22, 0.11); WOODL = (0.66, 0.45, 0.25); STONE = (0.56, 0.55, 0.53); STONED = (0.42, 0.41, 0.40)
BRICK = (0.76, 0.58, 0.40); BRICKD = (0.60, 0.44, 0.28); IRON = (0.30, 0.30, 0.33); IRONL = (0.55, 0.55, 0.60); HAY = (0.86, 0.74, 0.32); HAYD = (0.70, 0.58, 0.22)
FIRE = (1.0, 0.55, 0.12); BLACK = (0.08, 0.07, 0.07)

B = []
def add(n, vs, pal, v): B.append((n, vs, pal, v))
def ang(x, y): return math.degrees(math.atan2(y, x)) % 360


def yurt(R0=11.5, wall=7, felt='felt', feltD='feltD', band=('red', 'gold'), door='door', khan=False):
    v = {}
    Ri = int(R0) + 1
    for x in range(-Ri, Ri + 1):
        for y in range(-Ri, Ri + 1):
            d = math.hypot(x, y)
            if d > R0: continue
            a = ang(x, y)
            if d > R0 - 1.2:
                for z in range(0, wall):
                    k = felt
                    if z == wall - 2: k = band[0]
                    if z == wall - 3: k = band[1] if int(a // 12) % 2 == 0 else band[0]
                    if z == 1: k = 'rope'
                    if khan and z == 3 and int(a // 20) % 2 == 0: k = 'gold'
                    v[(x, y, z)] = k
            h = wall + (R0 - d) * 0.55
            zt = int(h)
            if d > 2.2:
                for z in range(wall, zt + 1):
                    if z >= zt - 1 or d > R0 - 1.5:
                        k = feltD if int(a // 30) % 2 else felt
                        if khan and z == zt and R0 - d < 2.6: k = 'red' if int(a // 10) % 2 == 0 else 'gold'
                        v[(x, y, z)] = k
            elif d > 1.2:
                v[(x, y, int(wall + (R0 - 2.2) * 0.55))] = 'wood'
    zc = int(wall + (R0 - 2.2) * 0.55)
    for i in range(-2, 3): v[(i, 0, zc)] = 'woodD'; v[(0, i, zc)] = 'woodD'
    for x in range(-2, 3):
        y0 = -int(math.sqrt(max(R0 ** 2 - x * x, 0)))
        for z in range(0, wall - 1):
            for yy in (y0, y0 + 1):
                if (x, yy, z) in v: v[(x, yy, z)] = door if not (z == wall - 2 or abs(x) == 2) else 'woodD'
    for z in range(1, wall - 2): v[(0, -int(R0), z)] = 'gold'
    if khan:   # horse-tail tug on the crown
        for z in range(zc + 1, zc + 7): v[(0, 0, z)] = 'woodD'
        for (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1)]: v[(dx, dy, zc + 5)] = 'black'; v[(dx, dy, zc + 4)] = 'black'
        v[(0, 0, zc + 7)] = 'gold'
    return v

PY = {'felt': FELT, 'feltD': FELTD, 'red': RED, 'gold': GOLD, 'wood': (0.55, 0.33, 0.16), 'woodD': WOODD, 'door': (0.72, 0.25, 0.12), 'rope': (0.35, 0.25, 0.15), 'blue': BLUE, 'grey': GREYF, 'greyD': (0.50, 0.48, 0.45), 'black': BLACK, 'white': WHITE}
add('Yurt_White', 0.25, PY, yurt())
add('Yurt_Blue', 0.25, PY, yurt(band=('blue', 'gold'), door='blue'))
add('Yurt_Grey', 0.25, PY, yurt(10.5, 6, 'grey', 'greyD', ('red', 'white')))
add('Yurt_Khan', 0.25, PY, yurt(15.5, 9, band=('red', 'gold'), khan=True))

# --- corral fence segment & gate (қора) ---
v = {}
for x in (0, 15): vbox(v, x, 0, 0, x, 1, 18, 'post')
for z in (6, 12): vbox(v, 0, 0, z, 15, 0, z + 1, 'rail')
add('Fence', 0.0625, {'post': WOODD, 'rail': WOOD}, v)
v = {}
for x in (0, 31): vbox(v, x, 0, 0, x + 1, 1, 26, 'post')
vbox(v, 0, 0, 25, 31, 1, 27, 'beam')
for x in range(4, 30, 4): v[(x, 0, 27)] = 'red'; v[(x + 1, 0, 27)] = 'gold'
for x in range(2, 30, 3): vbox(v, x, 0, 2, x, 0, 20, 'rail')
vbox(v, 2, 0, 10, 29, 0, 11, 'rail')
add('CorralGate', 0.0625, {'post': WOODD, 'beam': WOOD, 'rail': WOODL, 'red': RED, 'gold': GOLD}, v)

# --- well (құдық) ---
v = {}
for x in range(-6, 7):
    for y in range(-6, 7):
        d = math.hypot(x, y)
        if 4.2 <= d <= 6: vbox(v, x, y, 0, x, y, 6, 'stone' if hsh(x, y, 1) > -0.2 else 'stoneD')
        elif d < 4.2: v[(x, y, 1)] = 'water'
for x in (-5, 5): vbox(v, x, 0, 7, x, 0, 20, 'wood')
vbox(v, -5, 0, 20, 5, 0, 20, 'woodD'); vbox(v, -1, 0, 12, 1, 0, 15, 'bucket'); vbox(v, 0, 0, 16, 0, 0, 19, 'rope')
for x in range(-7, 8):
    for y in range(-3, 4): v[(x, y, 21 + (3 - abs(y)) // 2)] = 'roof' if abs(y) < 3 else 'red'
add('Well', 0.0625, {'stone': STONE, 'stoneD': STONED, 'water': (0.18, 0.42, 0.72), 'wood': WOOD, 'woodD': WOODD, 'bucket': (0.45, 0.30, 0.18), 'rope': (0.80, 0.70, 0.50), 'roof': WOODL, 'red': RED}, v)

# --- mausoleum (кесене) with ribbed turquoise dome ---
v = {}
S = 10
for x in range(-S, S + 1):
    for y in range(-S, S + 1):
        for z in range(0, 18):
            edge = abs(x) == S or abs(y) == S
            if not edge and z > 0: continue
            k = 'brick' if (z + (x + y) // 2) % 3 else 'brickD'
            if z in (14, 15): k = 'turq' if (x + y) % 2 else 'blue'
            if z == 16: k = 'gold' if (x + y) % 4 == 0 else 'brick'
            v[(x, y, z)] = k
# portal arch on -Y
for x in range(-4, 5):
    for z in range(0, 12):
        r = math.hypot(x, max(0, z - 7))
        if r < 3.6: v.pop((x, -S, z), None)
        elif r < 4.6: v[(x, -S, z)] = 'turq' if (x + z) % 2 else 'white'
for x in range(-6, 7): vbox(v, x, -S - 1, 0, x, -S - 1, 20, 'brick' if abs(x) > 5 else 'tile') if abs(x) >= 5 else None
for x in range(-6, 7):
    for z in (19, 20, 21): v[(x, -S - 1, z)] = 'tile' if (x + z) % 2 else 'turq'
# drum + dome
for z in range(18, 22):
    for a in range(0, 360, 4):
        r = 7.5; v[(round(r * math.cos(math.radians(a))), round(r * math.sin(math.radians(a))), z)] = 'tile' if z == 20 else 'brick'
for z in range(22, 34):
    t = (z - 22) / 11.0
    r = 8.0 * math.sqrt(max(0, 1 - t * t)) + 0.3
    for x in range(-9, 10):
        for y in range(-9, 10):
            d = math.hypot(x, y)
            if r - 1.3 <= d <= r:
                rib = int(ang(x, y) // 15) % 2 == 0
                v[(x, y, z)] = 'turq' if rib else 'turqL'
vbox(v, 0, 0, 34, 0, 0, 37, 'gold'); v[(1, 0, 37)] = 'gold'; v[(-1, 0, 37)] = 'gold'
add('Mausoleum', 0.25, {'brick': BRICK, 'brickD': BRICKD, 'turq': TURQ, 'turqL': TURQL, 'blue': BLUE, 'gold': GOLD, 'white': WHITE, 'tile': (0.15, 0.30, 0.70)}, v)

# --- caravanserai gate (керуен-сарай қақпасы) ---
v = {}
for x in range(-12, 13):
    for z in range(0, 26):
        for y in (0, 1, 2):
            v[(x, y, z)] = 'adobe' if (z + abs(x)) % 4 else 'adobeD'
for x in range(-6, 7):
    for z in range(0, 18):
        r = math.hypot(x, max(0, z - 12))
        if r < 5.6:
            for y in (0, 1, 2): v.pop((x, y, z), None)
        elif r < 6.8: v[(x, 0, z)] = 'turq' if (x + z) % 2 else 'white'
for x in range(-12, 13): v[(x, 0, 24)] = 'tile' if x % 2 else 'gold'; v[(x, 0, 25)] = 'turq'
for x in (-12, 12):
    for z in range(0, 30): v[(x, 1, z)] = 'adobeD'
    for z in (30, 31): v[(x, 1, z)] = 'turq'
add('CaravanseraiGate', 0.25, {'adobe': (0.80, 0.66, 0.46), 'adobeD': (0.66, 0.52, 0.34), 'turq': TURQ, 'white': WHITE, 'tile': (0.15, 0.30, 0.70), 'gold': GOLD}, v)

# --- crafting stations ---
v = {}   # workbench (верстак) with carved legs, tools on top
vbox(v, 0, 0, 12, 15, 15, 15, 'top'); vbox(v, 0, 0, 12, 15, 15, 12, 'topD')
for (x, y) in [(1, 1), (13, 1), (1, 13), (13, 13)]: vbox(v, x, y, 0, x + 1, y + 1, 11, 'leg')
for x in range(1, 15, 3): v[(x, 0, 14)] = 'carve'
vbox(v, 3, 4, 16, 9, 4, 16, 'iron'); vbox(v, 9, 3, 16, 10, 5, 17, 'iron')      # saw
vbox(v, 4, 10, 16, 4, 14, 16, 'woodL'); vbox(v, 3, 13, 16, 5, 14, 17, 'iron') # hammer
add('Workbench', 0.0625, {'top': WOODL, 'topD': WOOD, 'leg': WOODD, 'carve': RED, 'iron': IRON, 'woodL': WOODL}, v)
v = {}   # furnace / forge (пеш)
vbox(v, 0, 0, 0, 15, 15, 15, 'stone')
for p in list(v): v[p] = 'stone' if hsh(*p, 2) > -0.2 else 'stoneD'
for x in range(5, 11):
    for z in range(2, 8): v[(x, 0, z)] = 'fire' if z < 5 else 'glow'
vbox(v, 4, 0, 8, 11, 0, 8, 'iron')
vbox(v, 6, 6, 16, 9, 9, 22, 'stoneD')
add('Furnace', 0.0625, {'stone': STONE, 'stoneD': STONED, 'fire': FIRE, 'glow': (1.0, 0.85, 0.40), 'iron': IRON}, v)
v = {}   # anvil (төс) on a log
vbox(v, 3, 3, 0, 12, 12, 7, 'log'); vbox(v, 2, 5, 8, 13, 10, 9, 'iron'); vbox(v, 4, 6, 10, 11, 9, 11, 'iron')
vbox(v, 0, 6, 12, 15, 9, 14, 'ironL'); vbox(v, -2, 7, 13, -1, 8, 13, 'ironL')
add('Anvil', 0.0625, {'log': WOODD, 'iron': IRON, 'ironL': IRONL}, v)
v = {}   # loom (өрмек) with half-woven ornamental strip
for x in (0, 23): vbox(v, x, 0, 0, x, 1, 22, 'wood')
vbox(v, 0, 0, 22, 23, 1, 22, 'wood'); vbox(v, 0, 0, 3, 23, 1, 3, 'wood')
for x in range(2, 22):
    for z in range(4, 21):
        if z < 12: v[(x, 0, z)] = 'red' if (abs(x - 12) + abs(z - 8)) % 5 else 'gold'
        elif x % 2 == 0: v[(x, 0, z)] = 'thread'
add('Loom', 0.0625, {'wood': WOOD, 'red': RED, 'gold': GOLD, 'thread': (0.95, 0.93, 0.86)}, v)
v = {}   # tandyr oven
for z in range(0, 14):
    r = 7 - z * 0.25
    for x in range(-8, 9):
        for y in range(-8, 9):
            d = math.hypot(x, y)
            if d <= r and (d >= r - 1.4 or z == 0): v[(x, y, z)] = 'clay' if hsh(x, y, z) > -0.25 else 'clayD'
for x in range(-2, 3):
    for z in range(2, 6): v.pop((x, -7, z), None); v.pop((x, -6, z), None)
for x in range(-2, 3): v[(x, -5, 1)] = 'fire'
add('Tandyr', 0.0625, {'clay': (0.80, 0.62, 0.42), 'clayD': (0.66, 0.48, 0.30), 'fire': FIRE}, v)
v = {}   # market stall with striped awning and goods
for (x, y) in [(0, 0), (31, 0), (0, 15), (31, 15)]: vbox(v, x, y, 0, x, y, 26, 'post')
vbox(v, 0, 0, 10, 31, 15, 11, 'counter')
for x in range(0, 32):
    for y in range(-2, 18):
        z = 27 + (y + 2) // 6
        v[(x, y, z)] = 'red' if (x // 4) % 2 else 'white'
for (x, y, k) in [(4, 6, 'apple'), (6, 7, 'apple'), (10, 5, 'melon'), (11, 5, 'melon'), (16, 6, 'carpet'), (17, 6, 'carpet'), (18, 6, 'carpet'), (24, 6, 'pot'), (25, 6, 'pot')]:
    vbox(v, x, y, 12, x + 1, y + 2, 13, k)
add('MarketStall', 0.0625, {'post': WOODD, 'counter': WOODL, 'red': RED, 'white': WHITE, 'apple': (0.82, 0.14, 0.10), 'melon': (0.90, 0.78, 0.30), 'carpet': BLUE, 'pot': (0.75, 0.50, 0.30)}, v)
v = {}   # tug — horse-tail banner of the steppe
vbox(v, 0, 0, 0, 0, 0, 52, 'pole'); vbox(v, -1, -1, 0, 1, 1, 1, 'pole')
for z in range(53, 57): v[(0, 0, z)] = 'gold'
for (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1)]: v[(dx, dy, 55)] = 'gold'
for i in range(14):
    for (dx, dy) in [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1)]:
        v[(dx * (1 + i // 6), dy * (1 + i // 6), 52 - i)] = 'hair' if i < 12 else 'hairD'
for z in range(36, 46):
    for y in range(1, 13): v[(0, y, z)] = 'blue' if not (4 <= y <= 8 and 38 <= z <= 43) else 'gold'
add('Tug', 0.0625, {'pole': WOODD, 'gold': GOLD, 'hair': (0.10, 0.08, 0.06), 'hairD': (0.25, 0.20, 0.15), 'blue': (0.10, 0.60, 0.80)}, v)
v = {}   # haystack (шөмеле)
for z in range(0, 18):
    r = 9 * math.sqrt(max(0, 1 - (z / 18) ** 2)) + 0.5
    for x in range(-10, 11):
        for y in range(-10, 11):
            if math.hypot(x, y) + hsh(x, y, z) * 1.2 <= r: v[(x, y, z)] = 'hay' if hsh(x, y, z, 3) > -0.25 else 'hayD'
for i in range(-9, 10, 3): v[(i, 0, 15)] = 'rope'
add('Haystack', 0.0625, {'hay': HAY, 'hayD': HAYD, 'rope': WOODD}, v)

coll = get_coll('Buildings'); K['clear_coll'](coll)
BUILT = {}
for i, (n, vs, pal, v) in enumerate(B):
    o = voxel_mesh('SM_' + n, v, vs, 'T_Palette_' + n, pal, coll, loc=(-30 + (i % 5) * 12, 40 + (i // 5) * 14, 0))
    BUILT[n] = len(o.data.polygons)


for n in BUILT:
    K['export']([bpy.data.objects['SM_' + n]], K['RM'] + f"/Buildings/SM_{n}.fbx")
K['save_images']()
bpy.ops.wm.save_mainfile()
