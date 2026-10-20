"""DalaCraft — survival items: tool tiers (wood / stone / iron / gold), flint & steel, buckets, kese dishes, mob drops.
Each item = a 16px icon (Resources/Textures/Icons/Icon_<Item>.png) + the same pixels extruded into a 1/32 m voxel model
(Resources/Models/Items/SM_Item_<Item>.fbx, palette T_Palette_Item_<Item>), like Minecraft's held items.
Run inside Blender after km_tools.py:  exec(open(".../Blender/tools/items_survival.py").read())
"""
import bpy, os
import numpy as np

K = bpy.app.driver_namespace['km']
TEX, RM = K['TEX'], K['RM']
ICONS = TEX + "/Icons"


def load_icon(name):
    img = bpy.data.images.load(f"{ICONS}/Icon_{name}.png", check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a                                     # rows bottom-up


def save_icon(name, arr):
    h, w = arr.shape[:2]
    nm = "Icon_" + name
    img = bpy.data.images.get(nm) or bpy.data.images.new(nm, w, h, alpha=True)
    if tuple(img.size) != (w, h): img.scale(w, h)
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(arr.ravel())
    img.filepath_raw = f"{ICONS}/{nm}.png"; img.file_format = 'PNG'; img.save()


def grid(rows, pal):
    """rows top→bottom of 16 chars; '.' = transparent → bottom-up RGBA array"""
    a = np.zeros((16, 16, 4), np.float32)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == '.': continue
            c = pal[ch]
            a[15 - y, x] = (c[0] / 255, c[1] / 255, c[2] / 255, 1.0)
    return a


def recolor(src, mapping):
    out = src.copy()
    for (old, new) in mapping:
        o = np.array(old) / 255.0
        m = (np.abs(out[..., :3] - o).sum(axis=2) < 0.02) & (out[..., 3] > 0.5)
        out[m, :3] = np.array(new) / 255.0
    return out


def model(name, arr):
    """extrude icon pixels into a 1-voxel-thick model, one palette entry per colour"""
    vox, pal = {}, {}
    for y in range(16):
        for x in range(16):
            if arr[y, x, 3] < 0.5: continue
            key = 'c%02x%02x%02x' % tuple(int(round(v * 255)) for v in arr[y, x, :3])
            pal[key] = tuple(arr[y, x, :3])
            vox[(x, 0, y)] = key
    coll = K['get_coll']('Items')
    o = K['voxel_mesh']('SM_Item_' + name, vox, 1 / 32, 'T_Palette_Item_' + name, pal, coll, loc=(-30 + len(DONE) * 0.6, -78, 0.3))
    img = bpy.data.images['T_Palette_Item_' + name]
    img.filepath_raw = f"{TEX}/T_Palette_Item_{name}.png"; img.file_format = 'PNG'; img.save()
    K['export']([o], f"{RM}/Items/SM_Item_{name}.fbx")
    return o


DONE = []


def make(name, arr):
    save_icon(name, arr); model(name, arr); DONE.append(name)


# ------------------------------------------------------------------ tool tiers by recolouring the iron tools
IRON = [(242, 242, 250), (204, 204, 214), (128, 128, 140)]
TIERS = {
    'Wood': [(214, 168, 104), (170, 122, 68), (112, 78, 42)],
    'Stone': [(178, 178, 180), (134, 134, 138), (88, 88, 92)],
    'Gold': [(255, 240, 132), (246, 198, 60), (176, 124, 26)],
}
for tool in ('Pickaxe', 'Axe', 'Shovel'):
    base = load_icon(tool)
    for tier, cols in TIERS.items():
        make(tool + tier, recolor(base, list(zip(IRON, cols))))
ky = load_icon('Kylysh')
make('SwordWood', recolor(ky, list(zip(IRON[:2], TIERS['Wood'][:2])) + [((242, 191, 51), (112, 78, 42)), ((191, 31, 26), (90, 60, 30))]))
make('SwordStone', recolor(ky, list(zip(IRON[:2], TIERS['Stone'][:2])) + [((242, 191, 51), (110, 110, 114))]))
make('KylyshGold', recolor(ky, list(zip(IRON[:2], TIERS['Gold'][:2])) + [((191, 31, 26), (40, 110, 170))]))

# ------------------------------------------------------------------ new items
P = {
    'k': (28, 28, 32), 'g': (86, 88, 96), 'G': (140, 142, 152), 'w': (238, 238, 244), 'W': (200, 202, 212), 'i': (150, 152, 162),
    'r': (190, 40, 34), 'R': (120, 22, 18), 'o': (242, 150, 40), 'y': (252, 220, 90), 'b': (60, 120, 210), 'B': (120, 180, 240),
    'm': (250, 248, 238), 'M': (226, 222, 206), 'n': (150, 92, 50), 'N': (104, 62, 32), 'u': (196, 140, 80), 'U': (232, 200, 140),
    'e': (122, 150, 80), 'E': (88, 112, 58), 'p': (180, 222, 230), 'P': (120, 190, 210), 'c': (232, 184, 70), 'C': (198, 140, 40),
    't': (80, 52, 30), 'f': (255, 200, 80), 'F': (255, 120, 30), 'h': (210, 80, 20), 'l': (170, 110, 60),
}
make('Flint', grid([
    '................',
    '................',
    '.......kk.......',
    '.....kkgkkk.....',
    '....kgGgkkkk....',
    '....kGGgkgkk....',
    '...kkggkkGkkk...',
    '...kkkkkGGgkk...',
    '...kgkkkgGkkk...',
    '....kGgkkkkk....',
    '....kkGkkgkk....',
    '.....kkkkkk.....',
    '.......kk.......',
    '................',
    '................',
    '................'], P))
make('FlintSteel', grid([
    '................',
    '................',
    '..iiiiiiiii.....',
    '.iWWWWWWWWWi....',
    '.iW.......Wi....',
    '.iW........Wi...',
    '..i........Wi...',
    '...........Wi...',
    '.....kkk..Wi....',
    '....kGgkkWi.....',
    '...kGGgkk.......',
    '...kkggkk.......',
    '....kkkk........',
    '................',
    '................',
    '................'], P))
pail = [
    '................',
    '....iiiiiiii....',
    '...i........i...',
    '..i..........i..',
    '..WWWWWWWWWWWW..',
    '..W{}W..',
    '..iWWWWWWWWWWi..',
    '..iWWWWWWWWWWi..',
    '...iWWWWWWWWi...',
    '...iWWWWWWWWi...',
    '...iWWWWWWWWi...',
    '....iWWWWWWi....',
    '....iiiiiiii....',
    '................',
    '................',
    '................']
for name, fill in (('Bucket', 'gggggggggg'), ('BucketWater', 'bbBbbbbBbb'), ('BucketMilk', 'mmmMmmmmMm')):
    rows = [r.replace('{}', fill) if '{}' in r else r for r in pail]
    rows = [r if len(r) == 16 else (r + '................')[:16] for r in rows]
    make(name, grid(rows, P))
bowl = [
    '................',
    '................',
    '................',
    '................',
    '...wwwwwwwwww...',
    '..w{}w..',
    '..wrrrrrrrrrrw..',
    '..wwwwwwwwwwww..',
    '...wbwwbwwbwww..',
    '...wwwwwwwwww...',
    '....wwwwwwww....',
    '.....MMMMMM.....',
    '................',
    '................',
    '................',
    '................']
for name, fill in (('Kese', 'MMMMMMMMMM'), ('Sorpa', 'uuUuuuUuuu'), ('Kuyrdak', 'NnNNnNnNNn')):
    rows = [r.replace('{}', fill) if '{}' in r else r for r in bowl]
    rows = [(r + '................')[:16] for r in rows]
    make(name, grid(rows, P))
make('Beshbarmak', grid([
    '................',
    '................',
    '................',
    '.....NnN.NN.....',
    '...UUnNUUnNUU...',
    '..UUUUUUUUUUUU..',
    '.UUnNUUUNnUUUUU.',
    '.wUUUUUUUUUUUUw.',
    '.wwwwwwwwwwwwww.',
    '.wwrwwwrwwwrwww.',
    '..wwwwwwwwwwww..',
    '...MMMMMMMMMM...',
    '................',
    '................',
    '................',
    '................'], P))
make('String', grid([
    '................',
    '................',
    '..........mm....',
    '.........m..m...',
    '.........m......',
    '..........m.....',
    '...........m....',
    '.......mm..m....',
    '......m..mm.....',
    '.....m..........',
    '.....m..........',
    '......m.........',
    '.......mm.......',
    '.........m......',
    '................',
    '................'], P))
make('RottenFlesh', grid([
    '................',
    '................',
    '................',
    '......eEeee.....',
    '....eeEnneeee...',
    '...eenNNneEee...',
    '...eNNnnNNeee...',
    '...eenNNnneEe...',
    '....eeeNNeeee...',
    '....EeeeeEeee...',
    '.....eeEeeee....',
    '.......eee......',
    '................',
    '................',
    '................',
    '................'], P))
make('SpiderEye', grid([
    '................',
    '................',
    '................',
    '.....RRRRRR.....',
    '....RrrrrrrR....',
    '...RrrrrrrrrR...',
    '...Rrrkkkrrrr...',
    '...RrrkwkrrrR...',
    '...RrrkkkrrrR...',
    '...RrrrrrrrrR...',
    '....RrrrrrrR....',
    '.....RRRRRR.....',
    '................',
    '................',
    '................',
    '................'], P))
make('Ectoplasm', grid([
    '................',
    '................',
    '.......p........',
    '......pPp..p....',
    '.....pPmPp......',
    '..p..pmmmp......',
    '....pPmmmPp.....',
    '....pmmPmmp..p..',
    '.....pmmmp......',
    '......pPp.......',
    '..p....p...p....',
    '........p.......',
    '................',
    '................',
    '................',
    '................'], P))
make('FireEssence', grid([
    '................',
    '.......f........',
    '......fF........',
    '.....fFFf...f...',
    '....fFhFFf......',
    '....FFhhFF......',
    '...fFhhyhFf.....',
    '...FFhyyyhF.....',
    '...FhyymyyhF....',
    '...FhyymmyhF....',
    '....FhyyyyhF....',
    '.....FhhhhF.....',
    '......FFFF......',
    '................',
    '................',
    '................'], P))

ITEMS_DONE = DONE
