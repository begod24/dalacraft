"""DalaCraft — inventory icons.
  * every cube block → isometric 32px icon built from T_BlockAtlas (top / left / right faces shaded like Minecraft)
  * model blocks (workbench, furnace, sandyq, campfire, torch) → orthographic iso render of their Blender model
Output: Unity/Assets/Resources/Textures/Icons/Block_<BlockType>.png  (Items.MakeIcon picks them up).
Run inside Blender after km_tools.py:  exec(open(".../Blender/tools/icons.py").read())
"""
import bpy, math, os
import numpy as np
from mathutils import Vector

K = bpy.app.driver_namespace['km']
TEX = K['TEX']
ICONS = TEX + "/Icons"
os.makedirs(ICONS, exist_ok=True)

# tile indices from block_atlas.py (page * 64 + slot); Wood is the old name of the WoodSide tile
T = dict(bpy.app.driver_namespace['km_tiles']); T['Wood'] = T['WoodSide']
PAGES = ['T_BlockAtlas', 'T_BlockAtlas_Trees', 'T_BlockAtlas_Build']
BLOCKS = {
    'Grass': ('GrassTop', 'GrassSide'), 'DryGrass': ('DryTop', 'DrySide'), 'Dirt': ('Dirt', 'Dirt'), 'Stone': ('Stone', 'Stone'),
    'Snow': ('Snow', 'SnowSide'), 'Sand': ('Sand', 'Sand'), 'Water': ('Water', 'Water'), 'Felt': ('Felt', 'Felt'), 'Wood': ('WoodTop', 'Wood'),
    'Ice': ('Ice', 'Ice'), 'Gravel': ('Gravel', 'Gravel'), 'Clay': ('Clay', 'Clay'), 'Takyr': ('Takyr', 'Clay'), 'RedSand': ('RedSand', 'RedSand'),
    'RedStone': ('RedStone', 'RedStone'), 'OreCoal': ('OreCoal', 'OreCoal'), 'OreIron': ('OreIron', 'OreIron'), 'OreCopper': ('OreCopper', 'OreCopper'),
    'OreGold': ('OreGold', 'OreGold'), 'LeavesBroad': ('LeavesBroad', 'LeavesBroad'), 'LeavesNeedle': ('LeavesNeedle', 'LeavesNeedle'),
    'LeavesBirch': ('LeavesBirch', 'LeavesBirch'), 'Planks': ('Planks', 'Planks'), 'Birch': ('BirchTop', 'BirchSide'), 'FeltRed': ('FeltRed', 'FeltRed'),
    'FeltBlue': ('FeltBlue', 'FeltBlue'), 'Tekemet': ('Tekemet', 'Tekemet'), 'Shyrdak': ('Shyrdak', 'Shyrdak'), 'Adobe': ('Adobe', 'Adobe'),
    'TileTurquoise': ('TileTurquoise', 'TileTurquoise'), 'TileMajolica': ('TileMajolica', 'TileMajolica'), 'GoldOrnament': ('GoldOrnament', 'GoldOrnament'),
    'Cobble': ('Cobble', 'Cobble'), 'StoneBrick': ('StoneBrick', 'StoneBrick'), 'Sandstone': ('SandstoneTop', 'Sandstone'), 'Hay': ('HayTop', 'HaySide'),
    'Thatch': ('Thatch', 'Thatch'), 'WoolWhite': ('WoolWhite', 'WoolWhite'), 'WoolBrown': ('WoolBrown', 'WoolBrown'), 'MossStone': ('MossStone', 'MossStone'),
    'Bedrock': ('Bedrock', 'Bedrock'), 'Basalt': ('Basalt', 'Basalt'), 'Salt': ('Salt', 'Salt'),
    'LogSpruce': ('SpruceTop', 'SpruceSide'), 'LogPine': ('PineTop', 'PineSide'), 'LogSaxaul': ('SaxaulTop', 'SaxaulSide'),
    'LogTuranga': ('TurangaTop', 'TurangaSide'), 'LeavesAutumn': ('LeavesAutumn', 'LeavesAutumn'), 'LeavesBlossom': ('LeavesBlossom', 'LeavesBlossom'),
    'LeavesApple': ('LeavesApple', 'LeavesApple'), 'LeavesSnow': ('LeavesSnow', 'LeavesSnow'), 'LeavesTugai': ('LeavesTugai', 'LeavesTugai'),
    'LeavesSaxaul': ('LeavesSaxaul', 'LeavesSaxaul'), 'Glass': ('Glass', 'Glass'),
}
# flat sprite icons (Minecraft draws these items as 2D): block → tile(s), bottom to top
SPRITES = {'Ladder': ['Ladder'], 'Door': ['DoorBottom', 'DoorTop'],
           **{f'Sapling{s}': [f'Sapling{s}'] for s in ('Broad', 'Birch', 'Spruce', 'Pine', 'Apple', 'Saxaul', 'Tugai', 'Blossom')},
           **{n: [n] for n in ('MushroomRed', 'MushroomBrown', 'GlowShroom', 'CrystalTurquoise', 'CrystalAmethyst', 'CrystalRuby', 'LilyPad')},
           'Dripstone': ['DripTip']}
# shaped blocks rendered from their Blocks.fbx item mesh (page texture applied for the render)
SHAPED = [f'{k}{m}' for k in ('Slab', 'Stairs') for m in ('Cobble', 'Planks', 'StoneBrick', 'Sandstone', 'Adobe')] + ['Trapdoor']
MODEL_BLOCKS = {'Workbench': 'SM_Workbench', 'Furnace': 'SM_Furnace', 'Sandyq': 'SM_Sandyq', 'Campfire': 'SM_Campfire_Base', 'Torch': 'SM_Torch',
                'Bed': 'SM_Bed', 'Kazan': 'SM_KazanBlock', 'Kebeje': 'SM_Kebeje', 'Tandyr': 'SM_Tandyr'}


def load_atlas():
    """all atlas pages as arrays, row 0 = bottom (same as Unity)"""
    pages = []
    for name in PAGES:
        img = bpy.data.images.load(f"{TEX}/{name}.png", check_existing=True)
        img.reload()
        w, h = img.size
        pages.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
    return pages


def tile_px(atlas, tile, x, y_top):
    """pixel of a 16px tile; x left→right, y_top counted from the tile's top edge"""
    page, slot = divmod(tile, 64)
    tx, ty = slot % 8, slot // 8
    return atlas[page][ty * 16 + (15 - y_top), tx * 16 + x]


def sprite_icons():
    """ladders, doors and saplings: their tile(s) as a flat 32px sprite with a dark rim"""
    atlas = load_atlas()
    for block, tiles in SPRITES.items():
        h = 16 * len(tiles)
        src = np.zeros((h, 16, 4), np.float32)
        for i, t in enumerate(tiles):                       # bottom-up
            for y in range(16):
                for x in range(16): src[i * 16 + 15 - y, x] = tile_px(atlas, T[t], x, y)
        out = np.zeros((32, 32, 4), np.float32)
        k = 30 / h
        nh, nw = int(h * k), max(1, int(16 * k))
        ys = (np.arange(nh) / k).astype(int); xs = (np.arange(nw) / k).astype(int)
        out[1:1 + nh, (32 - nw) // 2:(32 - nw) // 2 + nw] = src[ys][:, xs]
        m = out[..., 3] > 0.05
        rim = np.zeros_like(m)
        rim[1:] |= m[:-1]; rim[:-1] |= m[1:]; rim[:, 1:] |= m[:, :-1]; rim[:, :-1] |= m[:, 1:]
        rim &= ~m
        out[rim] = (0.10, 0.06, 0.04, 0.85)
        save_icon("Block_" + block, out)


def shaped_icons():
    """slabs, stairs, trapdoor: iso render of the Blocks.fbx item mesh with its atlas page as texture"""
    for block in SHAPED:
        o = bpy.data.objects.get('SM_Block_' + block)
        if o is None: continue
        uv = o.data.uv_layers.get('Page')
        page = int(round(uv.data[0].uv[0])) if uv and len(uv.data) else 0
        img = bpy.data.images.load(f"{TEX}/{PAGES[page]}.png", check_existing=True)
        mat = K['make_mat']('M_IconPage%d' % page, img)
        old = list(o.data.materials)
        o.data.materials.clear(); o.data.materials.append(mat)
        try:
            model_icon(block, o.name)
        finally:
            o.data.materials.clear()
            for m in old: o.data.materials.append(m)


def iso_cube(atlas, top, side):
    """32x32 isometric cube: top rhombus 32x16, left/right faces 16x24, shaded 1.0 / 0.80 / 0.62."""
    out = np.zeros((32, 32, 4), np.float32)          # rows top→bottom here, flipped on save
    for Y in range(32):
        for X in range(32):
            px, py = X + 0.5, Y + 0.5
            u, v = (2 * py + px - 16) / 2, (2 * py - px + 16) / 2
            if 0 <= u < 16 and 0 <= v < 16:
                c = tile_px(atlas, top, int(v), int(u)).copy(); k = 1.0
            elif px < 16:
                hgt = 24 + 0.5 * px - py
                if not (0 <= hgt < 16): continue
                c = tile_px(atlas, side, int(px), 15 - int(hgt)).copy(); k = 0.80
            else:
                vv = 32 - px; hgt = 24 + 0.5 * vv - py
                if not (0 <= hgt < 16): continue
                c = tile_px(atlas, side, int(px - 16), 15 - int(hgt)).copy(); k = 0.62
            c[:3] *= k
            out[Y, X] = c
    # thin dark rim on the silhouette so icons read on any slot colour
    a = out[..., 3] > 0.05
    rim = np.zeros_like(a)
    rim[1:] |= a[:-1]; rim[:-1] |= a[1:]; rim[:, 1:] |= a[:, :-1]; rim[:, :-1] |= a[:, 1:]
    rim &= ~a
    out[rim] = (0.10, 0.06, 0.04, 0.85)
    return out[::-1].copy()


def save_icon(name, arr):
    h, w = arr.shape[:2]
    old = bpy.data.images.get(name)          # a FILE image would reload the 256px render written to the same path
    if old is not None: bpy.data.images.remove(old)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(arr.ravel())
    img.filepath_raw = f"{ICONS}/{name}.png"; img.file_format = 'PNG'; img.save()


def cube_icons():
    atlas = load_atlas()
    for b, (top, side) in BLOCKS.items():
        save_icon("Block_" + b, iso_cube(atlas, T[top], T[side]))


def load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a


def flame(h):
    """first frame of the fire flipbook (16x32, bottom-up) scaled to height h, nearest"""
    fb = load_rgba(TEX + "/T_FireFlipbook.png")[:, :16]
    ys = (np.arange(h) * 32 / h).astype(int); xs = (np.arange(max(2, h // 2)) * 16 / max(2, h // 2)).astype(int)
    return fb[ys][:, xs]


def paste(dst, src, x0, y0):
    """alpha-over src onto dst, (x0, y0) = bottom-left in bottom-up coords"""
    h, w = src.shape[:2]
    for y in range(h):
        for x in range(w):
            X, Y = x0 + x, y0 + y
            if 0 <= X < dst.shape[1] and 0 <= Y < dst.shape[0] and src[y, x, 3] > 0.05:
                a = src[y, x, 3]
                dst[Y, X, :3] = src[y, x, :3] * a + dst[Y, X, :3] * (1 - a)
                dst[Y, X, 3] = max(dst[Y, X, 3], a)


def finish(path, block, out=32):
    """crop the render to its silhouette, fit into out x out (nearest), brighten, add fire, dark rim"""
    a = load_rgba(path)
    ys, xs = np.where(a[..., 3] > 0.1)
    a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = a.shape[:2]
    inner = out - 2
    k = inner / max(h, w)
    nh, nw = max(1, int(round(h * k))), max(1, int(round(w * k)))
    a = a[(np.arange(nh) / k).astype(int).clip(0, h - 1)][:, (np.arange(nw) / k).astype(int).clip(0, w - 1)]
    a[..., :3] = np.clip(a[..., :3] * 1.3, 0, 1)
    img = np.zeros((out, out, 4), np.float32)
    y0, x0 = 1, (out - nw) // 2
    img[y0:y0 + nh, x0:x0 + nw] = a
    if block == 'Campfire':
        paste(img, flame(16), out // 2 - 4, 1 + nh // 3)
    elif block == 'Torch':
        paste(img, flame(10), x0 + nw // 2 - 2, y0 + nh - 4)
    m = img[..., 3] > 0.05
    rim = np.zeros_like(m)
    rim[1:] |= m[:-1]; rim[:-1] |= m[1:]; rim[:, 1:] |= m[:, :-1]; rim[:, :-1] |= m[:, 1:]
    rim &= ~m
    img[rim] = (0.10, 0.06, 0.04, 0.85)
    save_icon("Block_" + block, img)   # already bottom-up like Blender pixels


def model_icon(block, obj_name, size=256):
    """Orthographic isometric render of a model with flat studio light, transparent background."""
    src = bpy.data.objects[obj_name]
    sc = bpy.data.scenes.get("IconStudio") or bpy.data.scenes.new("IconStudio")
    for o in list(sc.collection.objects): sc.collection.objects.unlink(o)
    parts = [src] + [c for c in src.children if c.type == 'MESH']
    for o in parts: sc.collection.objects.link(o)
    bpy.context.view_layer.update()
    cam = bpy.data.objects.get("IconCam") or bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    bb = [o.matrix_world @ Vector(c) for o in parts for c in o.bound_box]
    lo = Vector((min(p.x for p in bb), min(p.y for p in bb), min(p.z for p in bb)))
    hi = Vector((max(p.x for p in bb), max(p.y for p in bb), max(p.z for p in bb)))
    ctr = (lo + hi) / 2
    d = Vector((1, -1, 0.82)).normalized()          # front-right view, Blender front = -Y
    cam.location = ctr + d * 20
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = (hi - lo).length * 1.0
    sc.camera = cam
    r = sc.render
    r.engine = 'BLENDER_WORKBENCH'; r.resolution_x = r.resolution_y = size; r.resolution_percentage = 100
    r.film_transparent = True; r.filter_size = 0.0
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_cavity = False; sh.show_shadows = False; sh.show_object_outline = False
    sc.display.render_aa = 'OFF'
    sc.view_settings.view_transform = 'Standard'
    r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
    r.filepath = f"{ICONS}/Block_{block}.png"
    win = bpy.context.window; prev = win.scene
    win.scene = sc
    try:
        bpy.ops.render.render(write_still=True, scene=sc.name)
    finally:
        win.scene = prev
        for o in parts: sc.collection.objects.unlink(o)
    finish(r.filepath, block)


def fence_models():
    """Voxel fence / gate (1/16 m) in the planks colours of the atlas — only used for their inventory icons."""
    atlas = load_atlas()
    tile = [tile_px(atlas, T['Planks'], x, y) for x in range(16) for y in range(16)]
    avg = np.mean([c[:3] for c in tile], axis=0)
    pal = {'p': tuple(avg), 'pD': tuple(avg * 0.78), 'pL': tuple(np.clip(avg * 1.15, 0, 1))}
    hsh = K['hsh']
    def key(p): h = hsh(*p, 5); return 'pL' if h > 0.3 else ('pD' if h < -0.3 else 'p')
    coll = K['get_coll']('Icons')
    fence, gate = {}, {}
    for px_ in (0, 12):                                  # two posts (4px) with rails between, like Minecraft's icon
        for x in range(px_, px_ + 4):
            for y in range(6, 10):
                for z in range(16): fence[(x, y, z)] = key((x, y, z))
    for z0 in (6, 12):
        for x in range(4, 12):
            for y in range(7, 9):
                for z in range(z0, z0 + 3): fence[(x, y, z)] = key((x, y, z))
    for x0 in (0, 14):                                   # gate: end posts, two rails, centre bars
        for x in range(x0, x0 + 2):
            for y in range(7, 9):
                for z in range(5, 16): gate[(x, y, z)] = key((x, y, z))
    for z0 in (6, 12):
        for x in range(2, 14):
            for y in range(7, 9):
                for z in range(z0, z0 + 3): gate[(x, y, z)] = key((x, y, z))
    for x in range(6, 10):
        for y in range(7, 9):
            for z in range(9, 12): gate[(x, y, z)] = key((x, y, z))
    K['voxel_mesh']('SM_Icon_Fence', fence, 1 / 16, 'T_Palette_Icon_Fence', pal, coll, loc=(-40, -120, 0))
    K['voxel_mesh']('SM_Icon_FenceGate', gate, 1 / 16, 'T_Palette_Icon_FenceGate', pal, coll, loc=(-38, -120, 0))
    bpy.context.view_layer.update()        # fresh objects need their world matrices before framing the camera


MODEL_BLOCKS['Fence'] = 'SM_Icon_Fence'
MODEL_BLOCKS['FenceGate'] = 'SM_Icon_FenceGate'


def model_icons():
    fence_models()
    for b, o in MODEL_BLOCKS.items():
        if o in bpy.data.objects: model_icon(b, o)


def build_all():
    cube_icons(); sprite_icons(); shaped_icons(); model_icons()


bpy.app.driver_namespace['km_icons'] = dict(build_all=build_all, cube_icons=cube_icons, model_icons=model_icons, model_icon=model_icon,
                                            sprite_icons=sprite_icons, shaped_icons=shaped_icons)
