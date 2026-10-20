"""Kazakh Mine — shared Blender helpers for building voxel assets and exporting them to Unity.

Load inside Blender:  exec(open(".../dalacraft/Blender/tools/km_tools.py").read())
Helpers are also registered in bpy.app.driver_namespace['km'].
"""
import bpy, bmesh, math, os
import numpy as np

ROOT = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/dalacraft"
UA = ROOT + "/Unity/Assets"
RM = UA + "/Resources/Models"
TEX = UA + "/Resources/Textures"


def hsh(*a):
    h = 0
    for v in a:
        h = (h * 1000003) ^ (int(v) & 0xffffffff)
        h &= 0xffffffff
    h ^= h >> 13; h = (h * 1274126177) & 0xffffffff; h ^= h >> 16
    return (h & 0xffff) / 65535.0 - 0.5


def mul(c, k): return (c[0] * k, c[1] * k, c[2] * k)


class Canvas:
    """Square tile atlas painter (tiles x tiles of ts px)."""
    def __init__(self, tiles=4, ts=16):
        self.tiles, self.ts = tiles, ts
        self.a = np.ones((tiles * ts, tiles * ts, 4), dtype=np.float32)

    def px(self, t, x, y, c, a=1.0):
        if 0 <= x < self.ts and 0 <= y < self.ts:
            tx, ty = t % self.tiles, t // self.tiles
            self.a[ty * self.ts + y, tx * self.ts + x, :3] = np.clip(c[:3], 0, 1)
            self.a[ty * self.ts + y, tx * self.ts + x, 3] = a

    def fill(self, t, c, noise=0.05, seed=0, a=1.0):
        for y in range(self.ts):
            for x in range(self.ts):
                n = hsh(t, x, y, seed) * noise * 2
                self.px(t, x, y, (c[0] + n, c[1] + n, c[2] + n), a)

    def rect(self, t, x, y, w, h, c):
        for i in range(w):
            for j in range(h): self.px(t, x + i, y + j, c)

    def diamond(self, t, cx, cy, r, c):
        for x in range(-r, r + 1):
            for y in range(-r, r + 1):
                if abs(x) + abs(y) == r: self.px(t, cx + x, cy + y, c)

    def to_image(self, name):
        n = self.tiles * self.ts
        img = bpy.data.images.get(name) or bpy.data.images.new(name, n, n, alpha=True)
        if img.size[0] != n: img.scale(n, n)
        img.alpha_mode = 'STRAIGHT'
        img.pixels.foreach_set(self.a.ravel())
        return img


def save_png(name, arr):
    h, w = arr.shape[:2]
    img = bpy.data.images.get(name) or bpy.data.images.new(name, w, h, alpha=True)
    if tuple(img.size) != (w, h): img.scale(w, h)
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(arr.ravel())
    os.makedirs(TEX, exist_ok=True)
    img.filepath_raw = f"{TEX}/{name}.png"; img.file_format = 'PNG'; img.save()
    img.filepath = img.filepath_raw
    return img


def make_mat(name, img):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); out.location = (400, 0)
    bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); bs.location = (100, 0)
    bs.inputs['Roughness'].default_value = 0.85
    if 'Specular IOR Level' in bs.inputs: bs.inputs['Specular IOR Level'].default_value = 0.2
    tx = nt.nodes.new('ShaderNodeTexImage'); tx.location = (-300, 0)
    tx.image = img; tx.interpolation = 'Closest'
    nt.links.new(tx.outputs['Color'], bs.inputs['Base Color'])
    nt.links.new(bs.outputs['BSDF'], out.inputs['Surface'])
    return m


# face corners BL,BR,TR,TL seen from outside (picks of min/max per axis)
FACES = {
    '-Y': [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
    '+Y': [(1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1)],
    '+X': [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
    '-X': [(0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1)],
    '+Z': [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)],
    '-Z': [(0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)],
}
CORNER_UV = [(0, 0), (1, 0), (1, 1), (0, 1)]
DIRS = {'+X': (1, 0, 0), '-X': (-1, 0, 0), '+Y': (0, 1, 0), '-Y': (0, -1, 0), '+Z': (0, 0, 1), '-Z': (0, 0, -1)}


def tile_rect(t, tiles=4, inset=0.002, v_lo=0.0, v_hi=1.0):
    s = 1.0 / tiles
    u0, v0 = (t % tiles) * s, (t // tiles) * s
    return (u0 + inset, v0 + inset + (s - 2 * inset) * v_lo, s - 2 * inset, (s - 2 * inset) * (v_hi - v_lo))


def add_box(bm, uv, center, size, tiles, dvert=None, gidx=None, side_v=(0.0, 1.0), skip=(), atlas_tiles=4):
    cx, cy, cz = center; sx, sy, sz = size
    lo = (cx - sx / 2, cy - sy / 2, cz - sz / 2); hi = (cx + sx / 2, cy + sy / 2, cz + sz / 2)
    for d, corners in FACES.items():
        if d in skip: continue
        vs = []
        for c in corners:
            v = bm.verts.new((hi[0] if c[0] else lo[0], hi[1] if c[1] else lo[1], hi[2] if c[2] else lo[2]))
            if dvert is not None: v[dvert][gidx] = 1.0
            vs.append(v)
        f = bm.faces.new(vs)
        vr = side_v if d[1] in 'XY' else (0.0, 1.0)
        u0, v0, w, h = tile_rect(tiles[d], atlas_tiles, v_lo=vr[0], v_hi=vr[1])
        for loop, (cu, cv) in zip(f.loops, CORNER_UV):
            loop[uv].uv = (u0 + cu * w, v0 + cv * h)


def all_t(t): return {d: t for d in FACES}


def get_coll(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


def clear_coll(c):
    for o in list(c.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def mesh_obj(name, bm, mat, coll, loc=(0, 0, 0)):
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    me = bpy.data.meshes.get(name)
    if me: bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    bm.normal_update(); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me); o.location = loc
    coll.objects.link(o)
    return o


def _palette_image(img_name, palette):
    keys = list(palette.keys()); n = 16
    arr = np.ones((n, n, 4), dtype=np.float32); slot = {}
    for i, k in enumerate(keys):
        for v in range(4):
            idx = i * 4 + v; x, y = idx % n, idx // n
            arr[y, x, :3] = np.clip(np.array(palette[k][:3]) * [0.92, 1.0, 1.06, 0.97][v], 0, 1)
            slot[(k, v)] = ((x + .5) / n, (y + .5) / n)
    img = bpy.data.images.get(img_name) or bpy.data.images.new(img_name, n, n, alpha=True)
    img.pixels.foreach_set(arr.ravel())
    return img, slot


def voxel_mesh(name, vox, vsize, img_name, palette, coll, loc=(0, 0, 0), origin=(0, 0, 0)):
    """vox: (x,y,z)->palette key. Exposed faces only; each voxel gets one of 4 shade variants."""
    img, slot = _palette_image(img_name, palette)
    mat = make_mat(img_name.replace('T_Palette_', 'M_'), img)
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap'); vmap = {}

    def V(p):
        v = vmap.get(p)
        if v is None:
            v = bm.verts.new(((p[0] - origin[0]) * vsize, (p[1] - origin[1]) * vsize, (p[2] - origin[2]) * vsize)); vmap[p] = v
        return v
    for (x, y, z), k in vox.items():
        u, vv = slot[(k, int((hsh(x, y, z, 7) + 0.5) * 4) % 4)]
        for d, off in DIRS.items():
            if (x + off[0], y + off[1], z + off[2]) in vox: continue
            try: f = bm.faces.new([V((x + c[0], y + c[1], z + c[2])) for c in FACES[d]])
            except ValueError: continue
            for l in f.loops: l[uv].uv = (u, vv)
    return mesh_obj(name, bm, mat, coll, loc)


def rigged_voxel_mesh(name, vox, vsize, img_name, palette, groups, coll):
    """vox: (x,y,z)->(key, group). Faces culled only against same-group neighbours; verts not shared across groups."""
    img, slot = _palette_image(img_name, palette)
    mat = make_mat(img_name.replace('T_Palette_', 'M_'), img)
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap'); dv = bm.verts.layers.deform.verify()
    gi = {g: i for i, g in enumerate(groups)}; vmap = {}

    def V(p, g):
        v = vmap.get((p, g))
        if v is None:
            v = bm.verts.new((p[0] * vsize, p[1] * vsize, p[2] * vsize)); v[dv][gi[g]] = 1.0; vmap[(p, g)] = v
        return v
    for (x, y, z), (k, g) in vox.items():
        u, vv = slot[(k, int((hsh(x, y, z, 7) + 0.5) * 4) % 4)]
        for d, off in DIRS.items():
            nb = vox.get((x + off[0], y + off[1], z + off[2]))
            if nb is not None and nb[1] == g: continue
            try: f = bm.faces.new([V((x + c[0], y + c[1], z + c[2]), g) for c in FACES[d]])
            except ValueError: continue
            for l in f.loops: l[uv].uv = (u, vv)
    old = bpy.data.objects.get(name)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    me = bpy.data.meshes.new(name); bm.normal_update(); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    o = bpy.data.objects.new(name, me); coll.objects.link(o)
    for g in groups: o.vertex_groups.new(name=g)
    return o


def vbox(vox, x0, y0, z0, x1, y1, z1, k):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            for z in range(z0, z1 + 1): vox[(x, y, z)] = k


def line(v, p0, p1, r0, r1, key):
    n = int(max(abs(p1[i] - p0[i]) for i in range(3)) * 2) + 1
    for s in range(n + 1):
        t = s / n; c = [p0[i] + (p1[i] - p0[i]) * t for i in range(3)]; r = r0 + (r1 - r0) * t
        ri = int(math.ceil(r))
        for dx in range(-ri, ri + 1):
            for dy in range(-ri, ri + 1):
                if dx * dx + dy * dy <= r * r + 0.35:
                    p = (round(c[0] + dx), round(c[1] + dy), round(c[2]))
                    v[p] = key(p) if callable(key) else key


def leafkey(dz_norm, p, seed, light='leafL', mid='leaf', dark='leafD', extra=None):
    h = hsh(p[0], p[1], p[2], seed)
    if extra and h > 0.42: return extra
    if dz_norm > 0.3 and h > -0.15: return light
    if dz_norm < -0.25 or h < -0.32: return dark
    return mid


def blob(v, c, rx, ry, rz, seed, keyfn=None, overwrite=False, rough=0.35, **kw):
    for x in range(int(c[0] - rx - 1), int(c[0] + rx + 2)):
        for y in range(int(c[1] - ry - 1), int(c[1] + ry + 2)):
            for z in range(int(c[2] - rz - 1), int(c[2] + rz + 2)):
                dx, dy, dz = (x - c[0]) / rx, (y - c[1]) / ry, (z - c[2]) / rz
                if dx * dx + dy * dy + dz * dz + hsh(x, y, z, seed) * rough <= 1:
                    p = (x, y, z)
                    if not overwrite and p in v: continue
                    v[p] = keyfn(dz, p) if keyfn else leafkey(dz, p, seed, **kw)


def sprinkle(v, key, prob, seed, only=('leaf', 'leafL', 'leafD')):
    for p, k in list(v.items()):
        if k in only and any((p[0] + d[0], p[1] + d[1], p[2] + d[2]) not in v for d in DIRS.values()):
            if hsh(p[0], p[1], p[2], seed) + 0.5 < prob: v[p] = key


def save_images():
    """Write every T_* image to Resources/Textures as PNG (Unity finds them by name from the FBX)."""
    os.makedirs(TEX, exist_ok=True)
    for img in bpy.data.images:
        if not img.name.startswith('T_'): continue
        path = f"{TEX}/{img.name}.png"
        img.filepath_raw = path; img.file_format = 'PNG'; img.save(); img.filepath = path
        if img.packed_file: img.unpack(method='REMOVE')


def export(objs, path, rig=None, actions=()):
    """Export objects to FBX for Unity (Y-up, -Z forward, 1 unit = 1 m). Each action becomes an NLA take."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    root = rig or objs[0]
    saved_loc, saved_rot = root.location.copy(), root.rotation_euler.copy()
    root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0)
    anim = bool(actions)
    if anim:
        ad = rig.animation_data or rig.animation_data_create()
        for tr in list(ad.nla_tracks): ad.nla_tracks.remove(tr)
        for a in actions:
            act = bpy.data.actions[a]
            tr = ad.nla_tracks.new(); tr.name = a
            st = tr.strips.new(a, int(act.frame_range[0]), act); st.name = a
        ad.action = None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        add_leaf_bones=False, bake_anim=anim, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=anim,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP', mesh_smooth_type='FACE')
    if anim:
        for tr in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(tr)
        rig.animation_data.action = bpy.data.actions[actions[0]]
    root.location, root.rotation_euler = saved_loc, saved_rot


bpy.app.driver_namespace['km'] = dict(
    hsh=hsh, mul=mul, Canvas=Canvas, save_png=save_png, make_mat=make_mat, FACES=FACES, DIRS=DIRS, tile_rect=tile_rect,
    add_box=add_box, all_t=all_t, get_coll=get_coll, clear_coll=clear_coll, mesh_obj=mesh_obj, voxel_mesh=voxel_mesh,
    rigged_voxel_mesh=rigged_voxel_mesh, vbox=vbox, line=line, leafkey=leafkey, blob=blob, sprinkle=sprinkle,
    save_images=save_images, export=export, RM=RM, TEX=TEX, UA=UA)
