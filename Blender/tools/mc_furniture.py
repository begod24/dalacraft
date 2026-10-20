"""DalaCraft — furniture block entities rebuilt Minecraft-style with calm textures (one base colour + one accent):
sandyq and kebeje (lids as hinged children), bed (корпе), workbench, furnace, campfire, dastarkhan, anvil.
Run after km_tools.py and mc_models.py:  exec(open(".../Blender/tools/mc_furniture.py").read())
"""
import bpy
M = bpy.app.driver_namespace['mc']; K = bpy.app.driver_namespace['km']
Model, solid, layered, planks, stone, metal, bark, border, stripes, mul = (
    M['Model'], M['solid'], M['layered'], M['planks'], M['stone'], M['metal'], M['bark'], M['border'], M['stripes'], M['mul'])
import numpy as np

WOOD, WOOD_D = (0.58, 0.40, 0.22), (0.40, 0.27, 0.13)
RED, RED_D, GOLD, SILVER = (0.66, 0.15, 0.12), (0.46, 0.09, 0.07), (0.88, 0.68, 0.24), (0.78, 0.78, 0.82)
COB = (0.50, 0.50, 0.52)
DONE = []


def dot(col, x, y, w=2, h=2):
    """a small rectangle accent at (x, y) from the face's bottom-left (negative = from the right / top)"""
    def p(W, H, s):
        a = np.zeros((H, W, 4), np.float32)
        x0 = x if x >= 0 else W + x; y0 = y if y >= 0 else H + y
        a[max(0, y0):y0 + h, max(0, x0):x0 + w] = (*col, 1)
        return a
    return p


def centred(col, w, h, dy=0):
    def p(W, H, s):
        a = np.zeros((H, W, 4), np.float32)
        x0 = (W - w) // 2; y0 = (H - h) // 2 + dy
        a[y0:y0 + h, x0:x0 + w] = (*col, 1)
        return a
    return p


def finish(m, coll, loc):
    o = m.build(K['get_coll'](coll), loc=loc); DONE.append(o.name); return o


def with_lid(body, lid, hinge, path, coll, loc):
    """body + lid child pivoting on the back hinge (lid boxes are given relative to the hinge)"""
    b = finish(body, coll, loc)
    old = bpy.data.objects.get(lid.name)
    l = lid.build(K['get_coll'](coll))
    l.parent = b; l.location = tuple(v / 16 for v in hinge)
    Model.export(b, path)
    return b


# ---------------------------------------------------------------- sandyq: painted red chest, gold edging, silver lock
body = Model('SM_Sandyq')
red = layered(solid(RED, 0.025), border(RED_D))
body.box((-7.5, -6, 1), (7.5, 6, 10), {'side': red, 'front': layered(red, centred(SILVER, 2, 3, 3)), 'bottom': solid(RED_D)})
for x in (-7.5, 5.5):
    for y in (-6, 4):
        body.box((x, y, 0), (x + 2, y + 2, 1), {'all': solid(WOOD_D)})                     # little feet
lid = Model('SM_Sandyq_Lid')                                                                # hinge at back top edge (y = +6, z = 10)
lid.box((-7.8, -12.3, 0), (7.8, 0.3, 4), {'side': layered(solid(RED, 0.025), stripes([GOLD] + [None] * 3)),
                                          'top': layered(solid(RED, 0.025), border(GOLD)), 'front': layered(solid(RED, 0.025), stripes([GOLD] + [None] * 3), centred(SILVER, 2, 2, -1))})
with_lid(body, lid, (0, 6, 10), '/Household/SM_Sandyq.fbx', 'Household', (-30, -62, 0))

# ---------------------------------------------------------------- kebeje: food chest on legs
body = Model('SM_Kebeje')
wood = planks(WOOD, 4)
body.box((-7, -5.5, 4), (7, 5.5, 14), {'side': wood, 'front': layered(wood, centred(GOLD, 2, 2)), 'bottom': solid(WOOD_D)})
for x in (-7, 5):
    for y in (-5.5, 3.5):
        body.box((x, y, 0), (x + 2, y + 2, 4), {'all': solid(WOOD_D)})
lid = Model('SM_Kebeje_Lid')
lid.box((-7.5, -11.8, 0), (7.5, 0.3, 2), {'side': solid(WOOD_D, 0.03), 'top': planks(WOOD_D, 4)})
with_lid(body, lid, (0, 5.5, 14), '/Household/SM_Kebeje.fbx', 'Household', (-19.5, -66, 0))

# ---------------------------------------------------------------- bed (корпе): wooden frame, folded blankets, pillow at the head (-Y)
m = Model('SM_Bed')
m.box((-8, -16, 0), (8, 16, 3), {'side': solid(WOOD_D), 'top': planks(WOOD, 4, True)})
m.box((-7.5, -15.5, 3), (7.5, 15.5, 7), {'side': layered(solid(RED, 0.02), stripes([None, GOLD, None, None])), 'top': solid(RED, 0.02)})
m.box((-7.5, -15.5, 7), (7.5, 9, 8), {'side': solid((0.20, 0.35, 0.62), 0.02), 'top': solid((0.20, 0.35, 0.62), 0.02)})    # quilt
m.box((-6, -15, 7), (6, -10, 10), {'all': solid((0.94, 0.93, 0.88), 0.02)})                                                  # pillow
finish(m, 'Household', (-26, -62, 0)); Model.export(bpy.data.objects['SM_Bed'], '/Household/SM_Bed.fbx')

# ---------------------------------------------------------------- workbench: planks with a craft grid on top
m = Model('SM_Workbench')
grid = layered(planks(WOOD, 4), stripes([None] * 5 + [WOOD_D] + [None] * 4 + [WOOD_D]), stripes([None] * 5 + [WOOD_D] + [None] * 4 + [WOOD_D], horizontal=False))
m.box((-8, -8, 0), (8, 8, 16), {'top': grid, 'side': layered(planks(WOOD_D, 4), stripes([None] * 12 + [WOOD] * 4)),
                               'front': layered(planks(WOOD_D, 4), stripes([None] * 12 + [WOOD] * 4), dot(SILVER, 3, 5, 1, 5), dot(SILVER, 2, 9, 3, 1))})
finish(m, 'Buildings', (-14, -66, 0)); Model.export(bpy.data.objects['SM_Workbench'], '/Buildings/SM_Workbench.fbx')

# ---------------------------------------------------------------- furnace: cobble block with a dark mouth
m = Model('SM_Furnace')
cob = stone(COB, 0.06)
m.box((-8, -8, 0), (8, 8, 16), {'side': cob, 'top': cob, 'front': layered(cob, centred((0.07, 0.07, 0.08), 8, 6, -2), centred((0.3, 0.3, 0.32), 10, 1, 2))})
finish(m, 'Buildings', (-12, -66, 0)); Model.export(bpy.data.objects['SM_Furnace'], '/Buildings/SM_Furnace.fbx')

# ---------------------------------------------------------------- campfire: crossed logs on a ring of stones
m = Model('SM_Campfire_Base')
for (x, y) in ((-7, -2), (5, -2), (-2, -7), (-2, 5), (-6, -6), (4, -6), (-6, 4), (4, 4)):
    m.box((x, y, 0), (x + 2.5, y + 2.5, 2), {'all': stone((0.56, 0.56, 0.58), 0.0)})
m.box((-6, -1.5, 1), (6, 1.5, 4), {'side': bark(WOOD_D), 'ends': solid(WOOD)})
m.box((-1.5, -6, 1), (1.5, 6, 4), {'side': bark(mul(WOOD_D, 0.9)), 'ends': solid(WOOD)}, rot=('z', 0, (0, 0, 0)))
m.box((-3, -3, 0), (3, 3, 0.5), {'top': solid((0.95, 0.45, 0.1), 0.1)})
finish(m, 'Household', (-24, -62, 0)); Model.export(bpy.data.objects['SM_Campfire_Base'], '/Household/SM_Campfire_Base.fbx')

# ---------------------------------------------------------------- dastarkhan: low table with a cloth and two kese
m = Model('SM_Dastarkhan')
m.box((-12, -12, 3), (12, 12, 5), {'top': layered(solid(RED, 0.02), border(GOLD)), 'side': solid(RED_D)})
for (x, y) in ((-11, -11), (9, -11), (-11, 9), (9, 9)): m.box((x, y, 0), (x + 2, y + 2, 3), {'all': solid(WOOD_D)})
for (x, y) in ((-5, -2), (3, 1)): m.box((x, y, 5), (x + 3, y + 3, 7), {'side': solid((0.95, 0.94, 0.9)), 'top': layered(solid((0.95, 0.94, 0.9)), centred((0.85, 0.75, 0.6), 1, 1))})
m.box((-1, -6, 5), (2, -3, 6), {'all': solid((0.85, 0.62, 0.32))})          # baursaks
finish(m, 'Household', (-22, -62, 0)); Model.export(bpy.data.objects['SM_Dastarkhan'], '/Household/SM_Dastarkhan.fbx')

# ---------------------------------------------------------------- anvil (Minecraft shape)
m = Model('SM_Anvil')
IR = (0.30, 0.30, 0.32)
m.box((-6, -5, 0), (6, 5, 4), {'all': metal(IR, False)})
m.box((-3, -2.5, 4), (3, 2.5, 9), {'all': metal(mul(IR, 0.9), False)})
m.box((-8, -4, 9), (8, 4, 15), {'side': metal(IR, False), 'top': metal(mul(IR, 1.25))})
finish(m, 'Buildings', (-10, -66, 0)); Model.export(bpy.data.objects['SM_Anvil'], '/Buildings/SM_Anvil.fbx')
MC_FURNITURE = DONE
