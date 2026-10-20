"""DalaCraft — underwater plants as instanced foliage (same cross-quad + atlas system as grass, so they sway and are cheap).
Paints 4 tiles into the free rows of T_FoliageAtlas (16x32 px tiles, 8 per row) and adds cross-quad meshes cloned from SM_Reed,
then re-exports Unity/Assets/Resources/Models/Foliage/Foliage.fbx with every plant.
Run after km_tools.py:  exec(open(".../Blender/tools/foliage_underwater.py").read())
"""
import bpy, math
import numpy as np
K = bpy.app.driver_namespace['km']; hsh = K['hsh']

TW, TH = 16, 32
# name -> (slot index, height in metres)
PLANTS = {'Seaweed_Tall': (16, 1.8), 'Seaweed_Short': (17, 0.8), 'Hornwort': (18, 1.2), 'Eelgrass': (19, 1.0)}

G = (0.20, 0.50, 0.22); GL = (0.38, 0.68, 0.30); GD = (0.10, 0.32, 0.16); TIP = (0.62, 0.78, 0.34); BR = (0.40, 0.42, 0.20)

img = bpy.data.images['T_FoliageAtlas']
W, H = img.size
A = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)


def tile(slot):
    col, row = slot % 8, slot // 8
    return col * TW, row * TH          # bottom-left pixel (Blender pixels are bottom-up)


def px(slot, x, y, c):
    if 0 <= x < TW and 0 <= y < TH:
        x0, y0 = tile(slot)
        A[y0 + y, x0 + x] = (c[0], c[1], c[2], 1.0)


def shade(x, y, s):
    h = hsh(x, y, s)
    return GL if h > 0.3 else (GD if h < -0.3 else G)


def clear(slot):
    x0, y0 = tile(slot); A[y0:y0 + TH, x0:x0 + TW] = 0


def ribbons(slot, n, top, seed, wide=True):
    clear(slot)
    for b in range(n):
        x0 = 3 + b * (10 // max(1, n - 1)) if n > 1 else 7
        h = top - int((hsh(b, seed) + 0.5) * 6)
        for y in range(h):
            x = round(x0 + math.sin(y * 0.35 + b * 1.7) * 1.6 * (y / h))
            c = TIP if y >= h - 2 else shade(x, y, seed + b)
            px(slot, x, y, c)
            if wide and y < h - 3: px(slot, x + 1, y, shade(x + 1, y, seed + 7))


ribbons(16, 3, 32, 1)                      # tall wavy seaweed
ribbons(17, 4, 15, 2)                      # short tuft
clear(18)                                  # hornwort: stem with whorls of needles
for y in range(24):
    px(18, 7, y, GD); px(18, 8, y, GD if y % 2 else G)
    if y % 3 == 1 and y > 1:
        r = 4 if y < 18 else 2
        for dx in range(1, r + 1):
            yy = y + (1 if dx > 2 else 0)
            px(18, 7 - dx, yy, GL if dx % 2 else G); px(18, 8 + dx, yy, GL if dx % 2 else G)
px(18, 7, 24, TIP); px(18, 8, 24, TIP)
clear(19)                                  # eelgrass: thin straight blades, brownish base
for b, (x, h) in enumerate([(2, 14), (4, 18), (6, 15), (8, 17), (10, 13), (12, 16), (14, 12)]):
    for y in range(h):
        px(19, x + (1 if y > h * 0.6 and b % 2 else 0), y, BR if y < 3 else (TIP if y >= h - 1 else shade(x, y, 40 + b)))

img.pixels.foreach_set(A.ravel())
img.update()
img.filepath_raw = K['TEX'] + "/T_FoliageAtlas.png"; img.file_format = 'PNG'; img.save()

# cross-quad meshes cloned from the reed, re-UV'd to the new tiles and scaled to height
coll = bpy.data.collections['Foliage']
reed = bpy.data.objects['SM_Reed']
for name, (slot, height) in PLANTS.items():
    old = bpy.data.objects.get('SM_' + name)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    me = reed.data.copy(); me.name = 'SM_' + name
    zmax = max(v.co.z for v in me.vertices) or 1
    for v in me.vertices: v.co.z *= height / zmax
    col, row = slot % 8, slot // 8
    u0, v0, du, dv = col / 8, row * TH / H, 1 / 8, TH / H
    uvl = me.uv_layers.active.data
    for d in uvl:
        fu = round((d.uv.x - 0.375) / 0.125); fv = round((d.uv.y - 0.25) / 0.25)   # reed tile → 0/1 corners
        d.uv = (u0 + fu * du, v0 + fv * dv)
    o = bpy.data.objects.new('SM_' + name, me); coll.objects.link(o)
    o.location = reed.location.copy(); o.location.x += 1.2 * (slot - 15)

objs = [o for o in coll.objects if o.type == 'MESH']
K['export'](objs, K['RM'] + "/Foliage/Foliage.fbx")
FOLIAGE_UW_DONE = sorted(o.name for o in objs)
