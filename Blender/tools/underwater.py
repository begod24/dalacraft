"""DalaCraft — algae-covered stones for lake and river beds (weeds are foliage: see foliage_underwater.py).
Run after km_tools.py:  exec(open(".../Blender/tools/underwater.py").read())
Exports to Unity/Assets/Resources/Models/Underwater/.
"""
import bpy, math
K = bpy.app.driver_namespace['km']; hsh, vbox, line, blob, voxel_mesh, get_coll = K['hsh'], K['vbox'], K['line'], K['blob'], K['voxel_mesh'], K['get_coll']

D = []
def add(n, vs, pal, v): D.append((n, vs, pal, v))

STONE = {'s': (0.46, 0.47, 0.46), 'sD': (0.33, 0.35, 0.35), 'sL': (0.58, 0.58, 0.55), 'moss': (0.26, 0.42, 0.22), 'mossL': (0.40, 0.56, 0.28)}


def wet_stone(rx, ry, rz, seed):
    v = {}
    def key(dz, p):
        h = hsh(*p, seed)
        if dz > 0.35 and h > -0.2: return 'mossL' if h > 0.3 else 'moss'   # algae on top
        return 'sL' if h > 0.3 else ('sD' if h < -0.25 else 's')
    blob(v, (0, 0, rz * 0.6), rx, ry, rz, seed, keyfn=key, rough=0.45)
    return {p: k for p, k in v.items() if p[2] >= 0}


add('RiverStone_1', 0.0625, STONE, wet_stone(7, 5, 4.5, 21))
add('RiverStone_2', 0.0625, STONE, wet_stone(4.5, 4, 3, 22))

coll = get_coll('Underwater'); K['clear_coll'](coll)
for i, (n, vs, pal, v) in enumerate(D):
    o = voxel_mesh('SM_' + n, v, vs, 'T_Palette_' + n, pal, coll, loc=(-30 + i * 3, -110, 0))
    K['export']([o], K['RM'] + f"/Underwater/SM_{n}.fbx")
K['save_images']()
UNDERWATER_DONE = [n for n, *_ in D]
