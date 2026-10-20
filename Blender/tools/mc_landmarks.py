"""DalaCraft — landmarks, cave treasure and the last voxel-style animals rebuilt Minecraft-style (calm 16px textures):
balbal, oba, ram skull, Tamgaly petroglyph rock, bones, gold pile, treasure chest, Altyn Adam crown, mausoleum, tug,
lantern, dombra; animals: tortoise and fish (сазан), rigged with their old clip names.
Run after km_tools.py, mc_models.py and mobs.py:  exec(open(".../Blender/tools/mc_landmarks.py").read())
"""
import bpy, math
M = bpy.app.driver_namespace['mc']; K = bpy.app.driver_namespace['km']
Model, solid, layered, stripes, border, mul, stone, planks, metal = M['Model'], M['solid'], M['layered'], M['stripes'], M['border'], M['mul'], M['stone'], M['planks'], M['metal']
patch, spots, rig, anims = M['patch'], M['spots'], M['rig'], M['anims']
R = math.radians; S = math.sin; TAU = 2 * math.pi
GREY, GREY_D, BONE, GOLD, GOLD_D, TURQ, WOOD, WOOD_D, RED, BLUE = (
    (0.56, 0.56, 0.57), (0.40, 0.40, 0.42), (0.88, 0.86, 0.80), (0.92, 0.72, 0.24), (0.70, 0.50, 0.14), (0.20, 0.62, 0.66),
    (0.58, 0.40, 0.22), (0.40, 0.27, 0.13), (0.68, 0.15, 0.12), (0.18, 0.32, 0.62))
DONE = []


def out(m, coll, path, loc=(0, 0, 0)):
    o = m.build(K['get_coll'](coll), loc=loc); Model.export(o, path); DONE.append(o.name); return o


# balbal: Turkic stone warrior — a stele with a carved face and a cup held at the chest
m = Model('SM_Balbal')
face = layered(stone(GREY, 0.04), patch(GREY_D, 1, 2, 2, 1), patch(GREY_D, 4, 2, 2, 1), patch(GREY_D, 1, 5, 5, 1), patch(GREY_D, 3, 3, 1, 2))
m.box((-4, -3, 0), (4, 3, 2), {'all': stone(GREY_D, 0.05)})
m.box((-3, -2, 2), (3, 2, 16), {'side': stone(GREY, 0.05), 'front': layered(stone(GREY, 0.05), patch(GREY_D, 1, 3, 4, 3)), 'top': stone(GREY)})
m.box((-3, -2.2, 16), (3, 2, 23), {'front': face, 'side': stone(GREY, 0.05), 'top': stone(GREY), 'back': stone(GREY, 0.05)})
out(m, 'Environment', '/Environment/SM_Balbal.fbx')

# oba: a cairn of stones with a pole and ribbons
m = Model('SM_Oba')
for (r, z0, z1, c) in ((10, 0, 4, GREY_D), (7, 4, 8, GREY), (4, 8, 11, GREY_D)):
    m.box((-r, -r, z0), (r, r, z1), {'side': stone(c, 0.12), 'top': stone(c, 0.1)})
m.box((-0.5, -0.5, 11), (0.5, 0.5, 30), {'all': solid(WOOD_D)})
for i, (col, y) in enumerate(((RED, 0.5), (BLUE, -1.5), ((0.95, 0.94, 0.9), 2.5), ((0.9, 0.75, 0.2), -3.5))):
    m.box((0.5, y, 22 - i * 2), (1.0, y + 1, 29 - i * 2), {'all': solid(col)}, rot=('y', 18 + i * 6, (0.5, y, 29 - i * 2)))
out(m, 'Environment', '/Environment/SM_Oba.fbx')

# ram skull (архар) with curled horns
m = Model('SM_RamSkull')
m.box((-3, -5, 0), (3, 3, 5), {'all': solid(BONE, 0.03), 'front': layered(solid(BONE), patch((0.2, 0.16, 0.14), 1, 1, 1, 1), patch((0.2, 0.16, 0.14), 4, 1, 1, 1))})
for sx in (1, -1):
    m.box((3 * sx - (0 if sx > 0 else 3), -1, 3), (3 * sx + (3 if sx > 0 else 0), 2, 6), {'all': layered(solid((0.80, 0.74, 0.62)), stripes([None, (0.62, 0.56, 0.46)]))})
    m.box((5 * sx - 1.5, 1, 0), (5 * sx + 1.5, 4, 4), {'all': layered(solid((0.80, 0.74, 0.62)), stripes([None, (0.62, 0.56, 0.46)]))})
out(m, 'Environment', '/Environment/SM_RamSkull.fbx')

# Tamgaly: dark varnished rock with light petroglyphs (sun-headed figure, argali)
VARN, PET = (0.30, 0.20, 0.14), (0.78, 0.62, 0.46)
glyphs = layered(stone(VARN, 0.05), patch(PET, 4, 2, 3, 3), patch(PET, 5, 5, 1, 6), patch(PET, 3, 7, 5, 1), patch(PET, 4, 11, 1, 3), patch(PET, 6, 11, 1, 3),
                 patch(PET, 13, 7, 6, 2), patch(PET, 13, 9, 1, 3), patch(PET, 18, 9, 1, 3), patch(PET, 19, 5, 1, 2), patch(PET, 20, 4, 2, 1))
m = Model('SM_PetroglyphRock')
m.box((-12, -7, 0), (12, 7, 14), {'front': glyphs, 'side': stone(VARN, 0.06), 'top': stone(mul(VARN, 1.15), 0.06)})
m.box((-8, -5, 14), (6, 5, 20), {'all': stone(VARN, 0.06)})
out(m, 'Environment', '/Environment/SM_PetroglyphRock.fbx')

# cave bones, gold pile, treasure chest, Altyn Adam's crown
m = Model('SM_Bones')
m.box((-2, -2, 0), (2, 2, 4), {'all': solid(BONE, 0.03), 'front': layered(solid(BONE), patch((0.2, 0.15, 0.12), 0, 1, 1, 1), patch((0.2, 0.15, 0.12), 2, 1, 1, 1))})
for (a, b, rz) in (((3, -1, 0), (13, 0, 1), 20), ((-12, 2, 0), (-3, 3, 1), -35), ((2, 4, 0), (4, 12, 1), 10)):
    m.box(a, b, {'all': solid(BONE, 0.03)}, rot=('z', rz, a))
out(m, 'Dungeon', '/Dungeon/SM_Bones.fbx')
m = Model('SM_GoldPile')
for (r, z0, z1) in ((8, 0, 2), (5, 2, 4), (2, 4, 5)):
    m.box((-r, -r + 1, z0), (r, r - 1, z1), {'side': layered(solid(GOLD, 0.06), spots(GOLD_D, 6, r)), 'top': layered(solid(mul(GOLD, 1.08), 0.06), spots((1, 0.95, 0.6), 5, r + 3))})
out(m, 'Dungeon', '/Dungeon/SM_GoldPile.fbx')
m = Model('SM_TreasureChest')
dark = layered(planks((0.30, 0.18, 0.10), 4), stripes([None] * 3 + [GOLD] + [None] * 3 + [GOLD] + [None] * 3))
m.box((-8, -6, 0), (8, 6, 10), {'side': dark, 'front': layered(dark, patch(GOLD, 7, 3, 2, 3)), 'top': solid((0.30, 0.18, 0.10))})
m.box((-8.3, -6.3, 10), (8.3, 6.3, 14), {'side': layered(solid((0.28, 0.16, 0.09)), stripes([GOLD, None, None, None])), 'top': layered(planks((0.30, 0.18, 0.10), 4), border(GOLD))})
out(m, 'Dungeon', '/Dungeon/SM_TreasureChest.fbx')
m = Model('SM_AltynAdamCrown')
for i, (r, z0, z1) in enumerate(((5, 0, 6), (4, 6, 12), (3, 12, 18), (2, 18, 24), (1, 24, 30))):
    m.box((-r, -r, z0), (r, r, z1), {'side': layered(solid(GOLD, 0.04), spots(TURQ, 2 + i, 50 + i)), 'top': solid(GOLD)})
for sx in (1, -1): m.box((4 * sx - 0.5, -0.5, 8), (4 * sx + 0.5, 0.5, 22), {'all': solid(mul(GOLD, 1.1))}, rot=('y', 15 * sx, (4 * sx, 0, 8)))   # arrows
out(m, 'Dungeon', '/Dungeon/SM_AltynAdamCrown.fbx')

# mausoleum (Yasawi style): sand-brick cube, portal with a turquoise frame, stepped turquoise dome
SANDB = (0.80, 0.66, 0.46)
brick = layered(solid(SANDB, 0.03), stripes([None, None, None, mul(SANDB, 0.82)]))
portal = layered(brick, patch(TURQ, 14, 6, 20, 34), patch((0.10, 0.08, 0.08), 18, 12, 12, 28))
m = Model('SM_Mausoleum')
m.box((-24, -24, 0), (24, 24, 40), {'side': brick, 'front': portal, 'top': solid(SANDB)})
for (r, z0, z1) in ((20, 40, 46), (16, 46, 52), (11, 52, 57), (6, 57, 60)):
    m.box((-r, -r, z0), (r, r, z1), {'side': layered(solid(TURQ, 0.04), stripes([None, mul(TURQ, 0.8)])), 'top': solid(mul(TURQ, 1.1))})
m.box((-1, -1, 60), (1, 1, 70), {'all': solid(GOLD)})
out(m, 'Buildings', '/Buildings/SM_Mausoleum.fbx')

# tug: banner pole with a horsetail and a crescent
m = Model('SM_Tug')
m.box((-0.75, -0.75, 0), (0.75, 0.75, 54), {'all': layered(solid(WOOD_D), stripes([None] * 10 + [RED]))})
m.box((-2.5, -2.5, 44), (2.5, 2.5, 50), {'all': layered(solid((0.12, 0.10, 0.09), 0.08), stripes([None, (0.22, 0.2, 0.18)], horizontal=False))})   # horsetail
m.box((-2, -0.5, 54), (2, 0.5, 58), {'all': solid(GOLD)})
m.box((0.75, -0.25, 34), (12, 0.25, 42), {'all': layered(solid(BLUE, 0.03), patch(GOLD, 4, 3, 3, 2))})
out(m, 'Buildings', '/Buildings/SM_Tug.fbx')

# lantern (шам) and dombra
m = Model('SM_Lantern')
m.box((-3, -3, 0), (3, 3, 1), {'all': metal((0.18, 0.18, 0.2), False)})
m.box((-2.5, -2.5, 1), (2.5, 2.5, 7), {'side': layered(solid((1.0, 0.82, 0.4)), border((0.18, 0.18, 0.2))), 'top': solid((0.18, 0.18, 0.2))})
m.box((-2, -2, 7), (2, 2, 8), {'all': metal((0.18, 0.18, 0.2), False)})
m.box((-0.5, -0.5, 8), (0.5, 0.5, 10), {'all': metal((0.18, 0.18, 0.2), False)})
out(m, 'Household', '/Household/SM_Lantern.fbx')
m = Model('SM_Dombra')
m.box((-3, -1.5, 0), (3, 1.5, 7), {'side': layered(planks(WOOD, 3, True), patch((0.15, 0.1, 0.06), 2, 2, 2, 2)), 'top': solid(WOOD)})
m.box((-1, -1, 7), (1, 0, 22), {'all': layered(solid(WOOD_D), stripes([None, None, (0.9, 0.9, 0.85)], horizontal=False))})
m.box((-1.5, -1.5, 22), (1.5, 0.5, 25), {'all': solid(WOOD_D)})
out(m, 'Household', '/Household/SM_Dombra.fbx')

# ---------------------------------------------------------------- animals: tortoise, fish
coll = K['get_coll']('Animals')
SHELL, SHELL_D, SKIN = (0.48, 0.42, 0.22), (0.34, 0.28, 0.14), (0.52, 0.48, 0.34)
m = Model('SM_Tortoise')
m.box((-4, -5, 1.5), (4, 5, 5), {'side': layered(solid(SHELL, 0.04), stripes([SHELL_D, None, None])), 'top': layered(solid(SHELL, 0.04), border(SHELL_D)), 'bottom': solid(SHELL_D)}, group='Body')
m.box((-1.5, -8, 1.5), (1.5, -5, 4), {'all': solid(SKIN, 0.03), 'front': layered(solid(SKIN), patch((0.05, 0.05, 0.05), 0, 1, 1, 1), patch((0.05, 0.05, 0.05), 2, 1, 1, 1))}, group='Head')
for (x, y, g) in ((2, -5, 'Leg.FL'), (-4, -5, 'Leg.FR'), (2, 3, 'Leg.BL'), (-4, 3, 'Leg.BR')):
    m.box((x, y, 0), (x + 2, y + 2, 2), {'all': solid(SKIN, 0.03)}, group=g)
bones = [('Root', (0, 0, 0), (0, 2, 0), None), ('Body', (0, 0, 3), (0, -3, 3), 'Root'), ('Head', (0, -5, 3), (0, -8, 3), 'Body'),
         ('Leg.FL', (3, -4, 2), (3, -4, 0), 'Body'), ('Leg.FR', (-3, -4, 2), (-3, -4, 0), 'Body'), ('Leg.BL', (3, 4, 2), (3, 4, 0), 'Body'), ('Leg.BR', (-3, 4, 2), (-3, 4, 0), 'Body')]
r, mesh = rig('Tortoise', m, bones, coll)
def t_walk(t, pb):
    s = S(t * TAU)
    pb['Leg.FL'].rotation_euler.x = R(20) * s; pb['Leg.BR'].rotation_euler.x = R(20) * s
    pb['Leg.FR'].rotation_euler.x = -R(20) * s; pb['Leg.BL'].rotation_euler.x = -R(20) * s
    pb['Head'].location.y = -0.01 * abs(s)
def t_idle(t, pb): pb['Head'].rotation_euler.z = R(10) * S(t * TAU)
def t_eat(t, pb): pb['Head'].rotation_euler.x = R(25 + 10 * S(t * TAU * 3))
K['export']([r, mesh], K['RM'] + '/Animals/Tortoise.fbx', r, anims(r, {'Idle': (48, t_idle), 'Walk': (32, t_walk), 'Run': (24, t_walk), 'Eat': (32, t_eat)}))
DONE.append('Tortoise')

CARP, CARP_D, FIN = (0.66, 0.52, 0.26), (0.48, 0.36, 0.16), (0.72, 0.38, 0.22)
m = Model('SM_Fish')
m.box((-1.5, -4, 1), (1.5, 3, 5), {'side': layered(solid(CARP, 0.04), stripes([CARP_D, None, None, (0.85, 0.78, 0.55)])), 'front': layered(solid(CARP), patch((0.05, 0.05, 0.05), 0, 1, 1, 1), patch((0.05, 0.05, 0.05), 2, 1, 1, 1)),
                                  'top': solid(CARP_D), 'bottom': solid((0.85, 0.78, 0.55))}, group='Body')
m.box((-0.5, -2, 5), (0.5, 1, 6.5), {'all': solid(FIN)}, group='Body')
m.box((-0.5, 3, 1.5), (0.5, 6, 4.5), {'all': solid(FIN)}, group='Tail')
bones = [('Root', (0, 0, 0), (0, 2, 0), None), ('Body', (0, -2, 3), (0, 2, 3), 'Root'), ('Tail', (0, 3, 3), (0, 6, 3), 'Body')]
r, mesh = rig('Fish', m, bones, coll)
def f_swim(t, pb): pb['Tail'].rotation_euler.z = R(28) * S(t * TAU); pb['Body'].rotation_euler.z = -R(6) * S(t * TAU)
def f_idle(t, pb): pb['Tail'].rotation_euler.z = R(10) * S(t * TAU)
K['export']([r, mesh], K['RM'] + '/Animals/Fish.fbx', r, anims(r, {'Idle': (40, f_idle), 'Swim': (16, f_swim)}))
DONE.append('Fish')
LANDMARKS_DONE = DONE
