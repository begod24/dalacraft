"""DalaCraft — household and aul models rebuilt Minecraft-style (cuboids + 16px painted faces, see mc_models.py):
kazan on its ошақ, tandyr, haystack, well, standing and wall torches.
Run after km_tools.py and mc_models.py:  exec(open(".../Blender/tools/mc_household.py").read())
"""
import bpy
M = bpy.app.driver_namespace['mc']
K = bpy.app.driver_namespace['km']
Model, solid, layered, planks, stone, clay, metal, bark, rings, border, stripes, koshkar, pixels, mul = (
    M['Model'], M['solid'], M['layered'], M['planks'], M['stone'], M['clay'], M['metal'], M['bark'], M['rings'], M['border'],
    M['stripes'], M['koshkar'], M['pixels'], M['mul'])

IRON, IRON_D = (0.16, 0.16, 0.17), (0.09, 0.09, 0.10)
WOOD, WOOD_D = (0.55, 0.36, 0.18), (0.38, 0.24, 0.11)
GOLD, RED, BLUE, WHITE = (0.90, 0.70, 0.22), (0.72, 0.13, 0.11), (0.16, 0.32, 0.62), (0.95, 0.93, 0.86)
CLAY = (0.80, 0.62, 0.43)
HAY = (0.86, 0.74, 0.34)
EMBER = layered(solid((0.18, 0.10, 0.07), 0.05), lambda w, h, s: __import__('numpy').array(
    [[(1.0, 0.45 + 0.3 * (K['hsh'](x, y, s) + 0.5), 0.1, 1.0) if K['hsh'](x, y, s + 1) > 0.05 else (0, 0, 0, 0) for x in range(w)] for y in range(h)], dtype='float32'))
DONE = []


def out(m, coll, path, loc):
    o = m.build(K['get_coll'](coll), loc=loc)
    Model.export(o, path)
    DONE.append(o.name)
    return o


# ---------------------------------------------------------------- kazan on its ошақ (fits one block)
m = Model('SM_KazanBlock')
for (x, y) in ((-7, -7), (6, -7), (-7, 6), (6, 6)):
    m.box((x, y, 0), (x + 1, y + 1, 6), {'all': metal(IRON_D, False)})                        # legs of the ошақ
m.box((-7, -7, 5), (7, -6, 6), {'all': metal(IRON)}); m.box((-7, 6, 5), (7, 7, 6), {'all': metal(IRON)})
m.box((-7, -6, 5), (-6, 6, 6), {'all': metal(IRON)}); m.box((6, -6, 5), (7, 6, 6), {'all': metal(IRON)})
m.box((-4, -4, 0), (4, 4, 1), {'top': EMBER, 'side': solid((0.2, 0.12, 0.08))})                # embers
m.box((-5, -1, 1), (5, 1, 2), {'side': bark(WOOD_D), 'ends': solid(WOOD)})                     # two small logs
m.box((-1, -5, 1), (1, 5, 2), {'side': bark(WOOD_D), 'ends': solid(WOOD)})
m.box((-4, -4, 3), (4, 4, 5), {'all': metal(IRON)})                                            # rounded pot: three tiers
m.box((-6, -6, 5), (6, 6, 11), {'side': layered(metal(IRON), stripes([None] * 4 + [mul(IRON, 1.6)])), 'bottom': metal(IRON)})
m.box((-6.5, -6.5, 11), (6.5, 6.5, 12), {'all': metal(mul(IRON, 1.3))})
for x in (-7.5, 6.5): m.box((x, -1, 9), (x + 1, 1, 11), {'all': metal(IRON_D)})                # ear handles
m.box((-5.5, -5.5, 12), (5.5, 5.5, 13), {'top': layered(planks(WOOD, 3), border(WOOD_D)), 'side': solid(WOOD_D)})   # wooden lid
m.box((-1, -1, 13), (1, 1, 14.5), {'all': solid(GOLD)})
out(m, 'Household', '/Household/SM_KazanBlock.fbx', (-22, -66, 0))

# ---------------------------------------------------------------- tandyr: clay oven, mouth on top, ash door at the front
m = Model('SM_Tandyr')
face = layered(clay(CLAY), stripes([None, mul(CLAY, 0.85)], size=1, offset=3))
m.box((-7, -7, 0), (7, 7, 5), {'side': face, 'top': clay(CLAY), 'bottom': clay(CLAY)})
m.box((-6, -6, 5), (6, 6, 10), {'side': clay(mul(CLAY, 1.04)), 'top': clay(CLAY)})
m.box((-4.5, -4.5, 10), (4.5, 4.5, 13), {'side': clay(mul(CLAY, 1.08)),
       'top': layered(solid((0.08, 0.05, 0.04)), border(mul(CLAY, 0.9), 2))})               # dark mouth with a rim
m.box((-3, -7.5, 1), (3, -7, 4), {'front': layered(solid((0.12, 0.07, 0.05)), border((0.35, 0.25, 0.18))), 'side': solid((0.3, 0.2, 0.14))})
m.box((-7.2, -7.2, 4), (7.2, 7.2, 5), {'side': layered(solid(RED), koshkar(GOLD, 7, 0)), 'top': solid(RED)})   # painted ornament band
out(m, 'Buildings', '/Buildings/SM_Tandyr.fbx', (-14, -70, 0))

# ---------------------------------------------------------------- haystack (шөмеле): stacked bales tied with rope
m = Model('SM_Haystack')
hay = lambda c: layered(solid(c, 0.07), stripes([None, mul(c, 0.82)] * 8, horizontal=False))
rope = stripes([None] * 5 + [(0.45, 0.30, 0.16)])
for i, (r, z0, z1) in enumerate(((9, 0, 7), (8, 7, 13), (6, 13, 17), (3, 17, 19))):
    m.box((-r, -r, z0), (r, r, z1), {'side': layered(hay(mul(HAY, 1 - 0.04 * i)), rope), 'top': hay(HAY), 'bottom': hay(HAY)})
out(m, 'Buildings', '/Buildings/SM_Haystack.fbx', (-10, -70, 0))

# ---------------------------------------------------------------- well (құдық): stone shaft, water, posts, roof, bucket
m = Model('SM_Well')
st = (0.55, 0.55, 0.57)
m.box((-8, -8, 0), (8, 8, 8), {'side': stone(st), 'top': layered(stone(st), lambda w, h, s: __import__('numpy').array(
    [[(0.12, 0.28, 0.55, 1.0) if 3 <= x < w - 3 and 3 <= y < h - 3 else (0, 0, 0, 0) for x in range(w)] for y in range(h)], dtype='float32'))})
for x in (-7, 5): m.box((x, -1, 8), (x + 2, 1, 24), {'all': bark(WOOD)})
m.box((-9, -9, 23), (9, 1, 25), {'top': planks(RED, 3), 'side': solid(mul(RED, 0.8)), 'bottom': planks(WOOD_D)}, rot=('x', 22.5, (0, -4, 24)))
m.box((-9, -1, 23), (9, 9, 25), {'top': planks(RED, 3), 'side': solid(mul(RED, 0.8)), 'bottom': planks(WOOD_D)}, rot=('x', -22.5, (0, 4, 24)))
m.box((-5, -0.5, 18), (5, 0.5, 19), {'all': bark(WOOD_D)})                                    # winch
m.box((-2, -2, 11), (2, 2, 15), {'side': layered(planks(WOOD, 2), stripes([None, IRON, None, None, IRON])), 'top': solid((0.12, 0.28, 0.55))})
m.box((-0.25, -0.25, 15), (0.25, 0.25, 18), {'all': solid((0.6, 0.55, 0.45))})               # rope
out(m, 'Buildings', '/Buildings/SM_Well.fbx', (-6, -70, 0))

# ---------------------------------------------------------------- torches (Minecraft size: 2x10 px, our red cloth wrap)
def torch(m, rot=None, z0=0, y=0):
    m.box((-1, y - 1, z0), (1, y + 1, z0 + 10), {'side': layered(bark((0.45, 0.30, 0.16)), stripes([None] * 7 + [RED, (0.15, 0.1, 0.08), (0.15, 0.1, 0.08)])),
                                          'top': solid((1.0, 0.78, 0.3)), 'bottom': solid((0.3, 0.2, 0.1))}, rot=rot)
m = Model('SM_Torch'); torch(m)
out(m, 'Household', '/Household/SM_Torch.fbx', (-20, -66, 0))
m = Model('SM_WallTorch')
torch(m, rot=('x', 22.5, (0, 5, 3)), z0=3, y=5)          # leans out of the wall (Blender +Y = wall)
m.box((-2, 7, 2), (2, 8, 8), {'all': metal(IRON)})                                               # wall plate
m.box((-1.5, 5, 4), (1.5, 7, 5), {'all': metal(IRON_D)})                                         # collar holding the torch
out(m, 'Dungeon', '/Dungeon/SM_WallTorch.fbx', (-18, -66, 0))
MC_HOUSEHOLD = DONE
