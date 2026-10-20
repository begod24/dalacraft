"""DalaCraft — Minecraft-style ("Blockbench") models: a few cuboids, every face painted with its own pixel texture at
16 px per metre, auto-packed into one texture per model (T_Palette_<Name>, so the Unity import rule links it).
This replaces the old "many tiny voxels" look: silhouettes stay blocky, detail lives in the pixels, like Minecraft.

    m = Model('SM_Tandyr')
    m.box((-7, -7, 0), (7, 7, 10), {'side': clay((0.78, 0.6, 0.42)), 'top': ...})
    obj = m.build(collection)          # mesh + material + texture
    m.export(obj, '/Buildings/SM_Tandyr.fbx')

Units: pixels (16 px = 1 m = one block). Blender axes: +Z up, -Y = front (= Unity +Z).
Face keys: top (+Z), bottom (-Z), front (-Y), back (+Y), left (-X), right (+X); 'side' = the four sides, 'all' = all.
Run after km_tools.py:  exec(open(".../Blender/tools/mc_models.py").read())
"""
import bpy, bmesh, math
import numpy as np

K = bpy.app.driver_namespace['km']
hsh = K['hsh']
PX = 1 / 16

FACE_KEY = {'+Z': 'top', '-Z': 'bottom', '-Y': 'front', '+Y': 'back', '-X': 'left', '+X': 'right'}


# ------------------------------------------------------------------ painters: (w, h, seed) -> float array [h][w][4], row 0 = bottom
def _arr(w, h): return np.zeros((h, w, 4), np.float32)


def solid(col, noise=0.05, alpha=1.0):
    def p(w, h, s):
        a = _arr(w, h)
        for y in range(h):
            for x in range(w):
                n = hsh(x, y, s) * noise * 2
                a[y, x] = (col[0] + n, col[1] + n, col[2] + n, alpha)
        return a
    return p


def layered(*painters):
    """paint several painters over each other (later ones over earlier where their alpha > 0)"""
    def p(w, h, s):
        out = _arr(w, h)
        for q in painters:
            a = q(w, h, s)
            m = a[..., 3] > 0.01
            out[m] = a[m]
        return out
    return p


def mul(c, k): return (c[0] * k, c[1] * k, c[2] * k)


def planks(col, board=4, vertical=False):
    def p(w, h, s):
        a = solid(col, 0.035)(w, h, s)
        for y in range(h):
            for x in range(w):
                u, v = (y, x) if vertical else (x, y)
                if v % board == board - 1: a[y, x, :3] = mul(col, 0.7)
                elif (u + (v // board) * 5) % 9 == 0: a[y, x, :3] = mul(col, 0.8)
                elif hsh(x, y, s + 3) > 0.42: a[y, x, :3] = mul(col, 1.12)
        return a
    return p


def stone(col, cracks=0.1):
    def p(w, h, s):
        a = solid(col, 0.06)(w, h, s)
        for y in range(h):
            for x in range(w):
                r = hsh(x // 2, y // 2, s + 7)
                if r > 0.38: a[y, x, :3] = mul(col, 0.84)
                elif r < -0.42: a[y, x, :3] = mul(col, 1.1)
                if hsh(x, y, s + 9) > 0.5 - cracks: a[y, x, :3] = mul(col, 0.66)
        return a
    return p


def clay(col):
    """smooth plaster with a few streaks (tandyr, saman walls)"""
    def p(w, h, s):
        a = solid(col, 0.03)(w, h, s)
        for x in range(w):
            if hsh(x, 0, s + 2) > 0.3:
                for y in range(h):
                    if hsh(x, y, s + 4) > -0.1: a[y, x, :3] = mul(col, 0.92)
        return a
    return p


def metal(col, shine=True):
    def p(w, h, s):
        a = solid(col, 0.025)(w, h, s)
        if shine:
            for y in range(h):
                for x in range(w):
                    if (x + y) % 7 == 0 and hsh(x, y, s) > 0: a[y, x, :3] = mul(col, 1.6)
        return a
    return p


def bark(col):
    def p(w, h, s):
        a = solid(col, 0.04)(w, h, s)
        for x in range(0, w, 3):
            for y in range(h):
                if hsh(x, y, s) > -0.3: a[y, x, :3] = mul(col, 0.72)
        return a
    return p


def rings(col, ring):
    def p(w, h, s):
        a = solid(col, 0.03)(w, h, s)
        cx, cy = (w - 1) / 2, (h - 1) / 2
        for y in range(h):
            for x in range(w):
                d = math.hypot(x - cx, y - cy)
                if int(d) % 2 == 1: a[y, x, :3] = ring
        return a
    return p


def border(col, width=1):
    def p(w, h, s):
        a = _arr(w, h)
        for y in range(h):
            for x in range(w):
                if x < width or y < width or x >= w - width or y >= h - width: a[y, x] = (*col, 1)
        return a
    return p


def stripes(cols, horizontal=True, size=1, offset=0):
    """horizontal (rows from the bottom) or vertical bands"""
    def p(w, h, s):
        a = _arr(w, h)
        for y in range(h):
            for x in range(w):
                i = ((y if horizontal else x) - offset) // size
                if 0 <= i < len(cols) and cols[i] is not None: a[y, x] = (*cols[i], 1)
        return a
    return p


KOSHKAR = [(0, 0), (-1, 1), (1, 1), (-2, 2), (2, 2), (-3, 1), (3, 1), (-3, 0), (3, 0), (-2, -1), (2, -1), (0, -1), (0, -2), (-1, -3), (1, -3)]


def koshkar(col, every=8, row=None):
    """қошқар мүйіз (ram horn) motifs repeated along the face; row = centre line from the bottom (default middle)"""
    def p(w, h, s):
        a = _arr(w, h)
        cy = h // 2 if row is None else row
        for cx in range(every // 2, w, every):
            for dx, dy in KOSHKAR:
                x, y = cx + dx, cy + dy
                if 0 <= x < w and 0 <= y < h: a[y, x] = (*col, 1)
        return a
    return p


def pixels(rows, pal):
    """explicit art: rows top→bottom, '.' transparent; stretched to the face with nearest sampling"""
    def p(w, h, s):
        a = _arr(w, h)
        H, W = len(rows), len(rows[0])
        for y in range(h):
            for x in range(w):
                ch = rows[H - 1 - int(y * H / h)][int(x * W / w)]
                if ch != '.': a[y, x] = (*pal[ch], 1)
        return a
    return p


# ------------------------------------------------------------------ model
class Model:
    def __init__(self, name):
        self.name = name
        self.boxes = []          # (lo, hi, faces, group, rot)

    def box(self, lo, hi, faces, group=None, rot=None, inflate=0.0):
        """faces: dict face-key → painter (or colour tuple). rot = (axis 'x'|'y'|'z', degrees, pivot px) like Blockbench."""
        f = {}
        for d, key in FACE_KEY.items():
            v = faces.get(key, faces.get('side' if key not in ('top', 'bottom') else 'ends', faces.get('all')))
            if v is None and key in ('top', 'bottom'): v = faces.get('all', faces.get('side'))
            if isinstance(v, tuple): v = solid(v)
            f[d] = v
        lo = tuple(c - inflate for c in lo); hi = tuple(c + inflate for c in hi)
        self.boxes.append((lo, hi, f, group, rot))
        return self

    def build(self, coll, groups=None, loc=(0, 0, 0)):
        corners = K['FACES']
        rects = []                                   # (w, h, painter, seed, box index, face dir)
        for bi, (lo, hi, f, g, rot) in enumerate(self.boxes):
            for d, cs in corners.items():
                if f[d] is None: continue
                pts = [tuple(hi[i] if c[i] else lo[i] for i in range(3)) for c in cs]
                w = max(1, int(round(math.dist(pts[0], pts[1]))))
                h = max(1, int(round(math.dist(pts[0], pts[3]))))
                rects.append([w, h, f[d], bi * 7 + len(rects), bi, d, pts])
        # shelf-pack the face images
        order = sorted(range(len(rects)), key=lambda i: -rects[i][1])
        W = 16
        while True:
            x = y = row = 0; ok = True; place = {}
            for i in order:
                w, h = rects[i][0], rects[i][1]
                if w > W: ok = False; break
                if x + w > W: x, y, row = 0, y + row, 0
                place[i] = (x, y); x += w; row = max(row, h)
            if ok and y + row <= W: break
            W *= 2
        tex = np.zeros((W, W, 4), np.float32)
        for i, (px_, py_) in place.items():
            w, h, painter, seed = rects[i][:4]
            tex[py_:py_ + h, px_:px_ + w] = painter(w, h, seed)
        img_name = 'T_Palette_' + self.name.replace('SM_', '')
        img = bpy.data.images.get(img_name)
        if img is not None and tuple(img.size) != (W, W):
            bpy.data.images.remove(img); img = None
        img = img or bpy.data.images.new(img_name, W, W, alpha=True)
        img.alpha_mode = 'STRAIGHT'
        img.pixels.foreach_set(np.clip(tex, 0, 1).ravel())
        img.filepath_raw = f"{K['TEX']}/{img_name}.png"; img.file_format = 'PNG'; img.save()
        mat = K['make_mat']('M_' + self.name.replace('SM_', ''), img)
        # mesh
        bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
        dv = bm.verts.layers.deform.verify() if groups else None
        gi = {g: i for i, g in enumerate(groups or [])}
        for i, r in enumerate(rects):
            w, h, _, _, bi, d, pts = r
            lo, hi, f, g, rot = self.boxes[bi]
            vs = []
            for p in pts:
                q = list(p)
                if rot:
                    ax, deg, piv = rot
                    a = math.radians(deg); c_, s_ = math.cos(a), math.sin(a)
                    i1, i2 = {'x': (1, 2), 'y': (2, 0), 'z': (0, 1)}[ax]
                    u, v = q[i1] - piv[i1], q[i2] - piv[i2]
                    q[i1], q[i2] = piv[i1] + u * c_ - v * s_, piv[i2] + u * s_ + v * c_
                vert = bm.verts.new((q[0] * PX, q[1] * PX, q[2] * PX))
                if dv is not None and g in gi: vert[dv][gi[g]] = 1.0
                vs.append(vert)
            face = bm.faces.new(vs)
            px_, py_ = place[i]
            for l, (cu, cv) in zip(face.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
                l[uv].uv = ((px_ + cu * w) / W, (py_ + cv * h) / W)
        o = K['mesh_obj'](self.name, bm, mat, coll, loc)
        for g in groups or []: o.vertex_groups.new(name=g)
        return o

    @staticmethod
    def export(o, rel_path, rig=None, actions=()):
        objs = [rig, o] if rig else [o] + [c for c in o.children]
        K['export'](objs, K['RM'] + rel_path, rig, actions)


bpy.app.driver_namespace['mc'] = dict(Model=Model, solid=solid, layered=layered, planks=planks, stone=stone, clay=clay, metal=metal,
                                      bark=bark, rings=rings, border=border, stripes=stripes, koshkar=koshkar, pixels=pixels, mul=mul)


def shot(names, path, d=(0.6, -1, 0.7), size=320, pose=None):
    """Quick orthographic workbench render of objects (and their mesh children) for review sheets."""
    from mathutils import Vector
    sc = bpy.data.scenes.get("IconStudio") or bpy.data.scenes.new("IconStudio")
    cam = bpy.data.objects.get("IconCam") or bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
    for o in list(sc.collection.objects): sc.collection.objects.unlink(o)
    objs = []
    for n in names:
        o = bpy.data.objects[n]; objs.append(o)
        objs += [c for c in o.children if c not in objs]
    for o in objs: sc.collection.objects.link(o)
    sc.collection.objects.link(cam)
    bpy.context.view_layer.update()
    meshes = [o for o in objs if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ctr = (lo + hi) / 2; dv = Vector(d).normalized()
    cam.location = ctr + dv * 40; cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = (hi - lo).length * 1.05; cam.data.clip_end = 200
    sc.camera = cam
    r = sc.render; r.engine = 'BLENDER_WORKBENCH'; r.resolution_x = r.resolution_y = size; r.film_transparent = False
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    r.filepath = path
    win = bpy.context.window; prev = win.scene; win.scene = sc
    try: bpy.ops.render.render(write_still=True, scene=sc.name)
    finally:
        win.scene = prev
        for o in objs: sc.collection.objects.unlink(o)
        sc.collection.objects.unlink(cam)


bpy.app.driver_namespace['mc']['shot'] = shot
