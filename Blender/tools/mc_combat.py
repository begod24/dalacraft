"""DalaCraft — combat bits: the flying arrow (Minecraft-style cuboids, pointing along Blender -Y = Unity +Z so the
projectile needs no extra rotation) and the small heart sprites drawn above creatures' heads:
red hearts for animals and villagers, dark "cursed" hearts for night mobs.
Run after km_tools.py and mc_models.py:  exec(open(".../Blender/tools/mc_combat.py").read())
"""
import bpy, os
import numpy as np
M = bpy.app.driver_namespace['mc']
K = bpy.app.driver_namespace['km']
Model, solid, stone, metal, stripes = M['Model'], M['solid'], M['stone'], M['metal'], M['stripes']

WOOD = (0.62, 0.45, 0.25)
FLINT = (0.30, 0.30, 0.32)
FEATHER = (0.93, 0.92, 0.88)
RED = (0.72, 0.13, 0.11)

# ---------------------------------------------------------------- flying arrow: shaft, flint head, cross of feathers
m = Model('SM_ArrowFlight')
m.box((-0.5, -6, -0.5), (0.5, 8, 0.5), {'all': solid(WOOD, 0.04)})                               # shaft
m.box((-1.5, -9, -0.5), (1.5, -6, 0.5), {'all': stone(FLINT, 0.0)})                               # head, flat
m.box((-0.5, -9, -1.5), (0.5, -6, 1.5), {'all': stone(FLINT, 0.0)})
m.box((-0.5, -10, -0.5), (0.5, -9, 0.5), {'all': solid((0.22, 0.22, 0.24), 0.02)})  # tip
feathers = stripes([FEATHER, RED], horizontal=False, size=2)
m.box((-2, 4, -0.25), (2, 8, 0.25), {'all': feathers})                                             # fletching, horizontal
m.box((-0.25, 4, -2), (0.25, 8, 2), {'all': feathers})                                             # fletching, vertical
o = m.build(K['get_coll']('Items'), loc=(-30, -80, 0))
Model.export(o, '/Items/SM_ArrowFlight.fbx')

# ---------------------------------------------------------------- hearts above heads (9x9, like the HUD ones)
OUT = K['UA'] + "/Resources/UI"
HEART = ["..##.##..", ".#######.", "#########", "#########", "#########", ".#######.", "..#####..", "...###...", "....#...."]


def heart(name, fill, outline, shine, empty_col, half=False, empty=False, crack=None):
    a = np.zeros((9, 9, 4), np.float32)
    for y, row in enumerate(HEART):
        for x, ch in enumerate(row):
            if ch != '#': continue
            edge = any(not (0 <= x + dx < 9 and 0 <= y + dy < 9) or HEART[y + dy][x + dx] != '#' for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            c = outline if edge else (empty_col if empty or (half and x > 4) else fill)
            a[8 - y, x] = (*c, 1)
    if not empty:
        a[8 - 2, 2] = (*shine, 1); a[8 - 2, 3] = (*shine, 1)
        if crack:   # a jagged crack down the middle of the cursed hearts
            for (x, y) in ((4, 3), (5, 4), (4, 5), (4, 6)):
                if not (half and x > 4): a[8 - y, x] = (*crack, 1)
    img = bpy.data.images.get(name)
    if img is not None: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, 9, 9, alpha=True)
    img.alpha_mode = 'STRAIGHT'; img.pixels.foreach_set(a.ravel())
    img.filepath_raw = f"{OUT}/{name}.png"; img.file_format = 'PNG'; img.save()


for suffix, kw in (('Full', {}), ('Half', {'half': True}), ('Empty', {'empty': True})):
    # mobs: soot-black hearts with a violet glint and a glowing ember crack (Шайтан's mark)
    heart('UI_MobHeart' + suffix, (0.17, 0.09, 0.20), (0.03, 0.02, 0.03), (0.62, 0.38, 0.78), (0.10, 0.08, 0.09), crack=(0.95, 0.42, 0.12), **kw)

result = {'arrow': o.name}
