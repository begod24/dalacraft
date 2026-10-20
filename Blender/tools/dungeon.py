"""Kazakh Mine — caves, mines and treasure props. Run after km_tools.py. Exports to Resources/Models/Dungeon/."""
import bpy, math
K = bpy.app.driver_namespace['km']; hsh, vbox, line, blob, voxel_mesh, get_coll = K['hsh'], K['vbox'], K['line'], K['blob'], K['voxel_mesh'], K['get_coll']
STONE = (0.50, 0.49, 0.48); STONED = (0.36, 0.35, 0.35); STONEL = (0.62, 0.61, 0.58); WET = (0.30, 0.32, 0.34)
GOLD = (0.95, 0.75, 0.22); GOLDD = (0.70, 0.50, 0.12); GOLDL = (1.0, 0.92, 0.55); WOOD = (0.48, 0.32, 0.18); WOODD = (0.32, 0.20, 0.11)
IRON = (0.32, 0.32, 0.35); RED = (0.72, 0.13, 0.11); BONE = (0.92, 0.88, 0.78)
D = []
def add(n, vs, pal, v): D.append((n, vs, pal, v))
def sk(p, s=0): h = hsh(*p, s); return 'sL' if h > 0.3 else ('sD' if h < -0.25 else 's')
SP = {'s': STONE, 'sD': STONED, 'sL': STONEL, 'wet': WET, 'tip': (0.75, 0.80, 0.85)}

def spike(height, base_r, hang, seed):
    v = {}
    for z in range(height):
        t = z / height
        r = base_r * (1 - t) ** 1.3 + 0.3
        for x in range(-int(base_r) - 1, int(base_r) + 2):
            for y in range(-int(base_r) - 1, int(base_r) + 2):
                if math.hypot(x, y) + hsh(x, y, z, seed) * 0.6 <= r:
                    zz = -z if hang else z
                    v[(x, y, zz)] = 'tip' if t > 0.85 else ('wet' if (z % 4 == 0) else sk((x, y, z), seed))
    return v
add('Stalactite_1', 0.125, SP, spike(22, 3.2, True, 1)); add('Stalactite_2', 0.125, SP, spike(14, 2.2, True, 2))
add('Stalagmite_1', 0.125, SP, spike(18, 3.6, False, 3)); add('Stalagmite_2', 0.125, SP, spike(10, 2.6, False, 4))

def crystals(col, colL, colD, seed):
    v = {}
    blob(v, (0, 0, 0), 4, 4, 1.5, seed, keyfn=lambda dz, p: sk(p, seed))
    for i in range(7):
        a = i * 0.9 + seed; tilt = 0.35 + (i % 3) * 0.15
        L = 7 + (i * 3) % 6; r0 = 1.2 if i % 2 else 0.9
        p0 = (math.cos(a) * 2, math.sin(a) * 2, 0.5); p1 = (p0[0] + math.cos(a) * L * tilt, p0[1] + math.sin(a) * L * tilt, L)
        line(v, p0, p1, r0, 0.3, lambda p: 'cL' if hsh(*p, 5) > 0.35 else ('cD' if hsh(*p, 6) < -0.3 else 'c'))
    return v
add('Crystal_Turquoise', 0.0625, dict(SP, c=(0.25, 0.85, 0.85), cL=(0.70, 1.0, 1.0), cD=(0.10, 0.50, 0.55)), crystals(0, 0, 0, 11))
add('Crystal_Amethyst', 0.0625, dict(SP, c=(0.60, 0.35, 0.85), cL=(0.85, 0.70, 1.0), cD=(0.35, 0.18, 0.55)), crystals(0, 0, 0, 12))
add('Crystal_Ruby', 0.0625, dict(SP, c=(0.85, 0.15, 0.20), cL=(1.0, 0.55, 0.55), cD=(0.50, 0.05, 0.10)), crystals(0, 0, 0, 13))

v = {}   # glowing cave mushrooms
for (cx, cy, h, r) in [(0, 0, 7, 3), (5, 2, 4, 2), (-4, 3, 5, 2), (2, -4, 3, 1.5)]:
    vbox(v, cx, cy, 0, cx, cy, h, 'stem')
    for x in range(-3, 4):
        for y in range(-3, 4):
            if x * x + y * y <= r * r: v[(cx + x, cy + y, h + 1)] = 'cap'
            if x * x + y * y <= (r - 1) ** 2: v[(cx + x, cy + y, h + 2)] = 'capL'
add('GlowMushrooms', 0.0625, {'stem': (0.70, 0.85, 0.90), 'cap': (0.20, 0.60, 0.95), 'capL': (0.55, 0.90, 1.0)}, v)

v = {}   # ancient treasure chest, lid open, full of gold
vbox(v, 0, 0, 0, 17, 11, 9, 'wood')
for x in (0, 17): vbox(v, x, 0, 0, x, 11, 9, 'gold')
for z in (0, 9): vbox(v, 0, 0, z, 17, 0, z, 'gold')
vbox(v, 1, 1, 9, 16, 10, 9, 'coin')
for i in range(30): v[(1 + int((hsh(i, 1) + 0.5) * 15), 1 + int((hsh(i, 2) + 0.5) * 9), 10 + int((hsh(i, 3) + 0.5) * 2))] = 'coin' if i % 3 else 'gem'
vbox(v, 0, 11, 10, 17, 12, 18, 'wood'); vbox(v, 0, 11, 18, 17, 12, 18, 'gold')
for x in (4, 13): v[(x, 0, 6)] = 'gem'
add('TreasureChest', 0.0625, {'wood': (0.40, 0.22, 0.10), 'gold': GOLD, 'coin': GOLDL, 'gem': (0.20, 0.75, 0.75)}, v)
v = {}   # gold coin pile
for x in range(-8, 9):
    for y in range(-8, 9):
        h = int(4 * math.exp(-(x * x + y * y) / 18.0) + hsh(x, y, 1) * 0.8)
        for z in range(0, max(h, 0) + 1): v[(x, y, z)] = 'coin' if hsh(x, y, z, 2) > -0.2 else 'coinD'
add('GoldPile', 0.0625, {'coin': GOLD, 'coinD': GOLDD}, v)

v = {}   # Golden Man (Алтын адам) tall headdress — legendary artefact
for z in range(0, 8):
    for x in range(-5, 6):
        for y in range(-5, 6):
            d = math.hypot(x, y)
            if 3.6 <= d <= 5: v[(x, y, z)] = 'gold' if (z + int(math.degrees(math.atan2(y, x)) // 30)) % 2 else 'goldD'
for z in range(8, 30):
    r = max(0.6, 4.2 - (z - 8) * 0.17)
    for x in range(-5, 6):
        for y in range(-5, 6):
            if math.hypot(x, y) <= r: v[(x, y, z)] = 'gold' if hsh(x, y, z) > -0.1 else 'goldL'
for i, z in enumerate(range(12, 28, 4)):     # arrow / feather plates
    for side in (-1, 1):
        for k in range(3): v[(side * (4 + k), 0, z + k)] = 'goldL'
for (x, y, z) in [(0, -5, 4), (0, -5, 5), (-1, -5, 5), (1, -5, 5)]: v[(x, y, z)] = 'turq'   # snow-leopard gem
v[(0, 0, 30)] = 'goldL'; v[(0, 0, 31)] = 'goldL'
add('AltynAdamCrown', 0.0625, {'gold': GOLD, 'goldD': GOLDD, 'goldL': GOLDL, 'turq': (0.20, 0.75, 0.75)}, v)

v = {}   # bones & skull
vbox(v, 0, 0, 0, 3, 3, 3, 'bone'); v[(1, 0, 2)] = 'dark'; v[(2, 0, 2)] = 'dark'; v[(1, 0, 0)] = 'dark'
for (a, b) in [((5, 0, 0), (13, 3, 0)), ((6, 5, 0), (12, 1, 0)), ((2, 6, 0), (9, 9, 0))]: line(v, a, b, 0.3, 0.3, 'bone')
for i in range(4): vbox(v, 14, 5 + i * 2, 0, 18, 5 + i * 2, 0, 'bone')
add('Bones', 0.0625, {'bone': BONE, 'dark': (0.15, 0.12, 0.10)}, v)

v = {}   # wall torch (flame is VFX in game)
vbox(v, 0, 0, 0, 1, 1, 9, 'wood'); vbox(v, -1, -1, 9, 2, 2, 10, 'iron'); vbox(v, 0, 0, 11, 1, 1, 11, 'ember')
vbox(v, -1, 1, 2, 2, 2, 4, 'iron')
add('WallTorch', 0.0625, {'wood': WOOD, 'iron': IRON, 'ember': (1.0, 0.55, 0.15)}, v)

v = {}   # mine cart with copper ore
for x in range(0, 16):
    for y in range(0, 10):
        for z in range(3, 11):
            if x in (0, 15) or y in (0, 9) or z == 3: v[(x, y, z)] = 'wood' if (x + z) % 5 else 'iron'
for (x, y) in [(2, -1), (13, -1), (2, 10), (13, 10)]: vbox(v, x - 1, y, 0, x + 1, y, 3, 'iron')
for x in range(1, 15):
    for y in range(1, 9): v[(x, y, 11 + (1 if hsh(x, y) > 0 else 0))] = 'ore' if hsh(x, y, 4) > -0.1 else 'oreG'
add('MineCart', 0.0625, {'wood': WOOD, 'iron': IRON, 'ore': (0.80, 0.45, 0.25), 'oreG': (0.35, 0.70, 0.55)}, v)
v = {}   # rail segment 1 m
for x in range(0, 16, 3): vbox(v, x, 0, 0, x + 1, 15, 0, 'tie')
for y in (3, 12): vbox(v, 0, y, 1, 15, y, 1, 'rail')
add('Rail', 0.0625, {'tie': WOODD, 'rail': IRON}, v)
v = {}   # mine support frame (крепь)
for x in (0, 30): vbox(v, x, 0, 0, x + 1, 1, 30, 'wood')
vbox(v, -1, 0, 30, 32, 1, 32, 'woodD')
for x in (3, 28): line(v, (x, 0, 26), (x + (3 if x < 10 else -3), 0, 30), 0.5, 0.5, 'woodD')
add('MineSupport', 0.0625, {'wood': WOOD, 'woodD': WOODD}, v)

coll = get_coll('Dungeon'); K['clear_coll'](coll)
for i, (n, vs, pal, v) in enumerate(D):
    o = voxel_mesh('SM_' + n, v, vs, 'T_Palette_' + n, pal, coll, loc=(-30 + (i % 8) * 3, -95 - (i // 8) * 4, 2.5 if n.startswith('Stalactite') else 0))
    K['export']([o], K['RM'] + f"/Dungeon/SM_{n}.fbx")
K['save_images']()
bpy.ops.wm.save_mainfile()
DUNGEON_DONE = [n for n, *_ in D]
