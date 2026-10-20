"""DalaCraft — builds the item meshes of every block (collection Blocks) and exports them as one FBX.
Used in game for dropped blocks and the block held in first-person hands (drawn with the voxel shader).
UVMap = position inside the 8x8 atlas page, second UV layer "Page" (u) = atlas page (Texture2DArray slice in Unity).
Cubes for full blocks; slabs, stairs, doors, trapdoors, ladders, fences, glass and crossed saplings get their own shapes.
Run after km_tools.py and block_atlas.py:  exec(open(".../Blender/tools/blocks_export.py").read())
"""
import bpy, bmesh
K = bpy.app.driver_namespace['km']
T = bpy.app.driver_namespace['km_tiles']

# (top, bottom, side) tile names — mirrors BlockInfo.Tile in Unity
def S(t): return (t, t, t)
CUBES = {
    'Grass': ('GrassTop', 'Dirt', 'GrassSide'), 'DryGrass': ('DryTop', 'Dirt', 'DrySide'), 'Dirt': S('Dirt'), 'Stone': S('Stone'),
    'Snow': ('Snow', 'Dirt', 'SnowSide'), 'Sand': S('Sand'), 'Water': S('Water'), 'Felt': S('Felt'), 'Wood': ('WoodTop', 'WoodTop', 'WoodSide'),
    'Ice': S('Ice'), 'Gravel': S('Gravel'), 'Clay': S('Clay'), 'Takyr': ('Takyr', 'Clay', 'Clay'), 'RedSand': S('RedSand'), 'RedStone': S('RedStone'),
    'OreCoal': S('OreCoal'), 'OreIron': S('OreIron'), 'OreCopper': S('OreCopper'), 'OreGold': S('OreGold'),
    'LeavesBroad': S('LeavesBroad'), 'LeavesNeedle': S('LeavesNeedle'), 'LeavesBirch': S('LeavesBirch'), 'Planks': S('Planks'),
    'Birch': ('BirchTop', 'BirchTop', 'BirchSide'), 'FeltRed': S('FeltRed'), 'FeltBlue': S('FeltBlue'), 'Tekemet': S('Tekemet'), 'Shyrdak': S('Shyrdak'),
    'Adobe': S('Adobe'), 'TileTurquoise': S('TileTurquoise'), 'TileMajolica': S('TileMajolica'), 'GoldOrnament': S('GoldOrnament'),
    'Cobble': S('Cobble'), 'StoneBrick': S('StoneBrick'), 'Sandstone': ('SandstoneTop', 'SandstoneTop', 'Sandstone'), 'Hay': ('HayTop', 'HayTop', 'HaySide'), 'Thatch': S('Thatch'),
    'WoolWhite': S('WoolWhite'), 'WoolBrown': S('WoolBrown'), 'MossStone': S('MossStone'), 'Bedrock': S('Bedrock'), 'Basalt': S('Basalt'), 'Salt': S('Salt'),
    'LogSpruce': ('SpruceTop', 'SpruceTop', 'SpruceSide'), 'LogPine': ('PineTop', 'PineTop', 'PineSide'),
    'LogSaxaul': ('SaxaulTop', 'SaxaulTop', 'SaxaulSide'), 'LogTuranga': ('TurangaTop', 'TurangaTop', 'TurangaSide'),
    'LeavesAutumn': S('LeavesAutumn'), 'LeavesBlossom': S('LeavesBlossom'), 'LeavesApple': S('LeavesApple'), 'LeavesSnow': S('LeavesSnow'),
    'LeavesTugai': S('LeavesTugai'), 'LeavesSaxaul': S('LeavesSaxaul'), 'Glass': S('Glass'),
}
SLABS = ['Cobble', 'Planks', 'StoneBrick', 'Sandstone', 'Adobe']
SAPLINGS = ['Broad', 'Birch', 'Spruce', 'Pine', 'Apple', 'Saxaul', 'Tugai', 'Blossom']
P = 1 / 16
FACE_KEYS = {'+Z': 'top', '-Z': 'bottom', '+X': 'side', '-X': 'side', '+Y': 'side', '-Y': 'side'}


class MeshBuilder:
    """Blender axes: +Z up, -Y = front (Unity +Z). Cells span 0..1; the mesh is centred on x/y."""
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.page = self.bm.loops.layers.uv.new('Page')

    def quad(self, corners, tile, uvs):
        vs = [self.bm.verts.new(c) for c in corners]
        f = self.bm.faces.new(vs)
        page, slot = divmod(tile, 64)
        u0, v0 = (slot % 8) / 8, (slot // 8) / 8
        for l, (u, v) in zip(f.loops, uvs):
            l[self.uv].uv = (u0 + 0.001 + u * (1 / 8 - 0.002), v0 + 0.001 + v * (1 / 8 - 0.002))
            l[self.page].uv = (page, 0)

    def box(self, lo, hi, tiles):
        """UVs projected from the position (keeps the 16px texel scale on thin parts, like the chunk mesher)"""
        for d, corners in K['FACES'].items():
            t = tiles.get(d, tiles.get(FACE_KEYS[d]))
            pts = [tuple(hi[i] if c[i] else lo[i] for i in range(3)) for c in corners]
            ax = 'XYZ'.index(d[1])
            ia, ib = (0, 1) if ax == 2 else ((1, 2) if ax == 0 else (0, 2))
            ca, cb = int(lo[ia]), int(lo[ib])            # the cell this box starts in (door top half = cell 1)
            uvs = [(min(max(p[ia] - ca, 0.0), 1.0), min(max(p[ib] - cb, 0.0), 1.0)) for p in pts]
            self.quad([(p[0] - 0.5, p[1] - 0.5, p[2]) for p in pts], T[t], uvs)

    def cross(self, tile):
        k = 0.15
        for (a, b) in [((k, k), (1 - k, 1 - k)), ((1 - k, k), (k, 1 - k))]:
            for side in (0, 1):
                p0, p1 = (a, b) if side == 0 else (b, a)
                cs = [(p0[0] - .5, p0[1] - .5, 0), (p1[0] - .5, p1[1] - .5, 0), (p1[0] - .5, p1[1] - .5, 1), (p0[0] - .5, p0[1] - .5, 1)]
                uvs = [(0, 0), (1, 0), (1, 1), (0, 1)] if side == 0 else [(1, 0), (0, 0), (0, 1), (1, 1)]
                self.quad(cs, T[tile], uvs)

    def finish(self, name, mat, coll):
        o = K['mesh_obj'](name, self.bm, mat, coll)
        me = o.data
        col = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
        col.data.foreach_set('color', [1.0] * (len(col.data) * 4))
        me.color_attributes.active_color = col
        me.uv_layers.active = me.uv_layers['UVMap']
        return o


coll = K['get_coll']('Blocks')
K['clear_coll'](coll)
mat = bpy.data.materials.get('M_Blocks') or bpy.data.materials.new('M_Blocks')
objs = []
for name, (top, bottom, side) in CUBES.items():
    mb = MeshBuilder(); mb.box((0, 0, 0), (1, 1, 1), {'top': top, 'bottom': bottom, 'side': side})
    objs.append(mb.finish('SM_Block_' + name, mat, coll))
for base in SLABS:
    tl = {'top': CUBES[base][0], 'bottom': CUBES[base][1], 'side': CUBES[base][2]}
    mb = MeshBuilder(); mb.box((0, 0, 0), (1, 1, 0.5), tl)
    objs.append(mb.finish('SM_Block_Slab' + base, mat, coll))
    mb = MeshBuilder(); mb.box((0, 0, 0), (1, 1, 0.5), tl); mb.box((0, 0.5, 0.5), (1, 1, 1), tl)      # back half up (Blender +Y = Unity -Z)
    objs.append(mb.finish('SM_Block_Stairs' + base, mat, coll))
mb = MeshBuilder()
mb.box((0, 0.5 - 1.5 * P, 0), (1, 0.5 + 1.5 * P, 1), {'top': 'DoorBottom', 'bottom': 'DoorBottom', 'side': 'DoorBottom'})
mb.box((0, 0.5 - 1.5 * P, 1), (1, 0.5 + 1.5 * P, 2), {'top': 'DoorTop', 'bottom': 'DoorTop', 'side': 'DoorTop'})
objs.append(mb.finish('SM_Block_Door', mat, coll))
mb = MeshBuilder(); mb.box((0, 0, 0), (1, 1, 3 * P), {'top': 'Trapdoor', 'bottom': 'Trapdoor', 'side': 'Trapdoor'})
objs.append(mb.finish('SM_Block_Trapdoor', mat, coll))
mb = MeshBuilder(); mb.box((0, 0.5 - 0.5 * P, 0), (1, 0.5 + 0.5 * P, 1), {'top': 'Ladder', 'bottom': 'Ladder', 'side': 'Ladder'})
objs.append(mb.finish('SM_Block_Ladder', mat, coll))
for s in SAPLINGS:
    mb = MeshBuilder(); mb.cross('Sapling' + s)
    objs.append(mb.finish('SM_Block_Sapling' + s, mat, coll))
for name, tile in (('MushroomRed', 'MushroomRed'), ('MushroomBrown', 'MushroomBrown'), ('GlowShroom', 'GlowShroom'), ('CrystalTurquoise', 'CrystalTurquoise'),
                   ('CrystalAmethyst', 'CrystalAmethyst'), ('CrystalRuby', 'CrystalRuby'), ('Dripstone', 'DripTip')):
    mb = MeshBuilder(); mb.cross(tile)
    objs.append(mb.finish('SM_Block_' + name, mat, coll))
mb = MeshBuilder(); mb.box((0, 0, 0), (1, 1, P), {'top': 'LilyPad', 'bottom': 'LilyPad', 'side': 'LilyPad'})
objs.append(mb.finish('SM_Block_LilyPad', mat, coll))
# fence: two posts with two rails (Minecraft's item model); gate: end posts + closed leaves
pl = {'top': 'Planks', 'bottom': 'Planks', 'side': 'Planks'}
mb = MeshBuilder()
for x0 in (0, 12 * P): mb.box((x0, 6 * P, 0), (x0 + 4 * P, 10 * P, 1), pl)
for z0 in (6 * P, 12 * P): mb.box((4 * P, 7 * P, z0), (12 * P, 9 * P, z0 + 3 * P), pl)
objs.append(mb.finish('SM_Block_Fence', mat, coll))
mb = MeshBuilder()
for x0 in (0, 14 * P): mb.box((x0, 7 * P, 5 * P), (x0 + 2 * P, 9 * P, 1), pl)
for z0 in (6 * P, 12 * P): mb.box((2 * P, 7 * P, z0), (14 * P, 9 * P, z0 + 3 * P), pl)
mb.box((6 * P, 7 * P, 9 * P), (10 * P, 9 * P, 12 * P), pl)
objs.append(mb.finish('SM_Block_FenceGate', mat, coll))
for i, o in enumerate(objs): o.location = (-60 + (i % 12) * 1.5, -100 - (i // 12) * 1.5, 0)
K['export'](objs, K['RM'] + "/Blocks/Blocks.fbx")
BLOCKS_EXPORTED = len(objs)
