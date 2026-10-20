"""DalaCraft — household block entities that need moving parts or a one-block size:
  * SM_Sandyq / SM_Kebeje: the lid is split off as a child object (…_Lid) pivoting on the back hinge, so the chest opens
  * SM_KazanBlock: a black cast-iron kazan with a wooden lid on an iron ошақ ring, embers underneath (fits one block)
  * SM_Torch (standing) and SM_WallTorch (leaning out from a back plate), Minecraft-sized
Run inside Blender after km_tools.py:  exec(open(".../Blender/tools/household_blocks.py").read())
"""
import bpy, bmesh, math
import numpy as np
K = bpy.app.driver_namespace['km']
RM = K['RM']


def darkest_uv(o):
    """UV of the darkest palette pixel used by an object (for inner faces)"""
    me = o.data
    img = next(n.image for n in me.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE')
    w, h = img.size
    px = np.array(img.pixels[:]).reshape(h, w, 4)
    uv = me.uv_layers.active.data
    best, buv = 9, (0.5, 0.5)
    for l in uv:
        u, v = l.uv
        c = px[min(h - 1, int(v * h)), min(w - 1, int(u * w))]
        s = c[0] + c[1] + c[2]
        if s < best: best, buv = s, (u, v)
    return buv


def split_lid(name, lid_z):
    body = bpy.data.objects[name]
    old = bpy.data.objects.get(name + '_Lid')
    if old:   # re-run: merge back first
        bpy.data.objects.remove(old, do_unlink=True)
    me = body.data
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    dark = darkest_uv(body)
    lid_faces = [f for f in bm.faces if min(v.co.z for v in f.verts) >= lid_z - 1e-4]
    if not lid_faces:
        bm.free(); return None
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    # lid mesh: copy lid faces, then delete them from the body
    lb = bmesh.new(); luv = lb.loops.layers.uv.new('UVMap'); vm = {}
    for f in lid_faces:
        vs = []
        for v in f.verts:
            key = tuple(round(c, 5) for c in v.co)
            if key not in vm: vm[key] = lb.verts.new(v.co)
            vs.append(vm[key])
        try: nf = lb.faces.new(vs)
        except ValueError: continue
        for a, b in zip(nf.loops, f.loops): a[luv].uv = b[uvl].uv
    bmesh.ops.delete(bm, geom=lid_faces, context='FACES')
    # caps: the dark inside of the box and the underside of the lid
    lx0 = min(v.co.x for v in lb.verts); lx1 = max(v.co.x for v in lb.verts); ly0 = min(v.co.y for v in lb.verts); ly1 = max(v.co.y for v in lb.verts)
    def cap(b, uvlay, pts):
        vs = [b.verts.new(p) for p in pts]
        f = b.faces.new(vs)
        for l in f.loops: l[uvlay].uv = dark
    t = 1 / 16
    cap(bm, uvl, [(x0 + t, y0 + t, lid_z - t), (x1 - t, y0 + t, lid_z - t), (x1 - t, y1 - t, lid_z - t), (x0 + t, y1 - t, lid_z - t)])
    cap(lb, luv, [(lx0, ly0, lid_z), (lx0, ly1, lid_z), (lx1, ly1, lid_z), (lx1, ly0, lid_z)])
    bm.to_mesh(me); bm.free()
    # pivot on the back hinge (Blender +Y = back of the chest)
    hinge = ((lx0 + lx1) / 2, ly1, lid_z)
    for v in lb.verts: v.co.x -= hinge[0]; v.co.y -= hinge[1]; v.co.z -= hinge[2]
    lme = bpy.data.meshes.get(name + '_Lid')
    if lme: bpy.data.meshes.remove(lme)
    lme = bpy.data.meshes.new(name + '_Lid'); lb.normal_update(); lb.to_mesh(lme); lb.free()
    lme.materials.append(me.materials[0])
    lid = bpy.data.objects.new(name + '_Lid', lme)
    for c in body.users_collection: c.objects.link(lid)
    lid.parent = body
    lid.location = hinge
    return lid


def export_with_children(o, path):
    K['export']([o] + list(o.children), path)


DONE = []
for name, lid_z, path in (('SM_Sandyq', 0.5625, '/Household/SM_Sandyq.fbx'), ('SM_Kebeje', 0.9375, '/Household/SM_Kebeje.fbx')):
    if bpy.data.objects.get(name + '_Lid') is None:
        split_lid(name, lid_z)
    export_with_children(bpy.data.objects[name], RM + path)
    DONE.append(name)

coll = K['get_coll']('Household')

# ---------------------------------------------------------------- kazan on its ошақ (one block, 1/16 voxels)
pal = {'k': (0.10, 0.10, 0.11), 'kL': (0.22, 0.22, 0.24), 'kH': (0.34, 0.34, 0.37), 'r': (0.28, 0.27, 0.27), 'rD': (0.16, 0.15, 0.15),
       'w': (0.58, 0.38, 0.19), 'wD': (0.42, 0.26, 0.12), 'g': (0.86, 0.66, 0.22), 'c': (0.13, 0.09, 0.07), 'e': (0.98, 0.48, 0.10),
       'eY': (1.0, 0.78, 0.30), 'l': (0.40, 0.27, 0.15)}
v = {}
C = 7.5
for x in range(16):
    for y in range(16):
        dx, dy = x - C, y - C; r = math.hypot(dx, dy)
        # embers and two small logs on the ground
        if r < 3.2: v[(x, y, 0)] = 'e' if K['hsh'](x, y, 3) > 0.05 else ('eY' if K['hsh'](x, y, 4) > 0.2 else 'c')
        if abs(dy) < 0.8 and abs(dx) < 4.5: v[(x, y, 1)] = 'l'
        if abs(dx) < 0.8 and abs(dy) < 4.5: v[(x, y, 1)] = 'l'
        # iron ring of the ошақ at z 5
        if 6.4 <= r < 7.6: v[(x, y, 5)] = 'r'
        # the pot: a rounded bowl from z 4 to 12
        for z in range(4, 13):
            pr = 3.5 + 3.2 * math.sin(min(1.0, (z - 3.2) / 5.5) * math.pi / 2)
            if z == 12: pr = 6.8                         # rim
            if r < pr and (r > pr - 1.2 or z <= 4):
                v[(x, y, z)] = 'kH' if (dx < -2 and dy < -2 and z in (8, 9)) else ('kL' if K['hsh'](x, y, z) > 0.25 else 'k')
        # wooden lid with a knob
        if r < 6.2: v[(x, y, 13)] = 'w' if (x + y) % 4 else 'wD'
        if r < 1.2: v[(x, y, 14)] = 'g'
# three legs of the ring
for a in (90, 210, 330):
    lx, ly = C + 7 * math.cos(math.radians(a)), C + 7 * math.sin(math.radians(a))
    for z in range(0, 6): v[(int(round(lx)), int(round(ly)), z)] = 'rD'
# two ear handles
for x in (0, 15): v[(x, 7, 11)] = 'kL'; v[(x, 8, 11)] = 'kL'
kz = K['voxel_mesh']('SM_KazanBlock', v, 1 / 16, 'T_Palette_KazanBlock', pal, coll, loc=(-22, -66, 0), origin=(8, 8, 0))
img = bpy.data.images['T_Palette_KazanBlock']; img.filepath_raw = K['TEX'] + '/T_Palette_KazanBlock.png'; img.file_format = 'PNG'; img.save()
K['export']([kz], RM + '/Household/SM_KazanBlock.fbx'); DONE.append('SM_KazanBlock')

# ---------------------------------------------------------------- torches
tp = {'s': (0.45, 0.30, 0.16), 'sD': (0.33, 0.21, 0.11), 'cl': (0.70, 0.14, 0.12), 'h': (0.15, 0.10, 0.08), 'f': (1.0, 0.62, 0.15),
      'fY': (1.0, 0.86, 0.40), 'i': (0.20, 0.20, 0.22), 'iL': (0.32, 0.32, 0.35)}
def torch_vox(tilt=False):
    v = {}
    for z in range(0, 10):
        for x in (7, 8):
            for y in (7, 8):
                v[(x, y, z)] = 'sD' if (x + y + z) % 3 == 0 else 's'
    for x in (7, 8):
        for y in (7, 8):
            v[(x, y, 7)] = 'cl'; v[(x, y, 8)] = 'h'; v[(x, y, 9)] = 'h'; v[(x, y, 10)] = 'f'
    v[(7, 7, 10)] = 'fY'
    return v
t1 = K['voxel_mesh']('SM_Torch', torch_vox(), 1 / 16, 'T_Palette_Torch', tp, coll, loc=(-20, -66, 0), origin=(8, 8, 0))
img = bpy.data.images['T_Palette_Torch']; img.filepath_raw = K['TEX'] + '/T_Palette_Torch.png'; img.file_format = 'PNG'; img.save()
K['export']([t1], RM + '/Household/SM_Torch.fbx'); DONE.append('SM_Torch')

# wall torch: the same torch leaning 22° out of an iron bracket fixed on the wall (Blender +Y = wall side)
wv = torch_vox()
for x in range(6, 10):
    for z in range(1, 6): wv[(x, 12, z)] = 'i'           # wall plate
for y in range(9, 12): wv[(7, y, 3)] = 'iL'; wv[(8, y, 3)] = 'iL'   # arm to the torch
old = bpy.data.objects.get('SM_WallTorch')
if old: bpy.data.objects.remove(old, do_unlink=True)
t2 = K['voxel_mesh']('SM_WallTorch', wv, 1 / 16, 'T_Palette_WallTorch', tp, K['get_coll']('Dungeon'), loc=(-18, -66, 0), origin=(8, 8, 0))
# lean the torch (not the plate) away from the wall: rotate verts with y < 0.20 about the arm
me = t2.data
pivot = (0.0, 1 / 16, 3.5 / 16)
ang = math.radians(22)        # top swings to -Y, away from the wall
for vt in me.vertices:
    if vt.co.y < 0.07:
        y, z = vt.co.y - pivot[1], vt.co.z - pivot[2]
        vt.co.y = pivot[1] + y * math.cos(ang) - z * math.sin(ang)
        vt.co.z = pivot[2] + y * math.sin(ang) + z * math.cos(ang)
me.update()
img = bpy.data.images['T_Palette_WallTorch']; img.filepath_raw = K['TEX'] + '/T_Palette_WallTorch.png'; img.file_format = 'PNG'; img.save()
K['export']([t2], RM + '/Dungeon/SM_WallTorch.fbx'); DONE.append('SM_WallTorch')
HOUSEHOLD_DONE = DONE
