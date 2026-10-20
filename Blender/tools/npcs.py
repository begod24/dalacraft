"""Kazakh Mine — villagers / NPCs. Same rig layout as the player (Root, Hips, Spine, Head, Arm.L/R, Leg.L/R).
Run inside Blender after km_tools.py. Exports to Resources/Models/NPC/<Name>.fbx with Idle/Walk/Talk/Work takes.
"""
import bpy, bmesh, math
K = bpy.app.driver_namespace['km']; Canvas, mul, hsh, get_coll = K['Canvas'], K['mul'], K['hsh'], K['get_coll']
R = math.radians; S = math.sin; TAU = 2 * math.pi
TILE = dict(Skin=0, Face=1, HeadBack=2, HatSide=3, HatTop=4, TorsoFront=5, TorsoSide=6, TorsoBack=7, Sleeve=8, Trouser=9, Boot=10,
            Skirt=11, Prop=12, Sole=13, HeadSide=14, Hand=15)
RED = (0.72, 0.13, 0.11); GOLD = (0.90, 0.70, 0.22); WHITE = (0.96, 0.95, 0.92); BLACK = (0.07, 0.07, 0.08); BLUE = (0.16, 0.32, 0.62)


def koshkar(c, t, cx, cy, col):
    for (dx, dy) in [(0, 0), (-1, 1), (1, 1), (-2, 2), (2, 2), (-3, 1), (3, 1), (0, -1), (0, -2)]: c.px(t, cx + dx, cy + dy, col)


def paint(spec):
    """spec: dict of colours / options -> 4x4 16px Canvas."""
    c = Canvas(); T = TILE
    skin = spec.get('skin', (0.86, 0.64, 0.47)); hair = spec.get('hair', (0.08, 0.06, 0.05))
    c.fill(T['Skin'], skin, 0.025); c.fill(T['Hand'], skin, 0.03)
    # face
    f = T['Face']; c.fill(f, skin, 0.025)
    if spec.get('frame'):          # kimeshek: white cloth frames the face
        fr = spec['frame']; c.rect(f, 0, 12, 16, 4, fr); c.rect(f, 0, 0, 2, 16, fr); c.rect(f, 14, 0, 2, 16, fr); c.rect(f, 0, 0, 16, 2, fr)
        for x in range(2, 14, 2): c.px(f, x, 12, RED)
    else:
        c.rect(f, 0, 13, 16, 3, hair if not spec.get('bald') else skin)
    brow = spec.get('brow', hair)
    c.rect(f, 3, 11, 4, 1, brow); c.rect(f, 9, 11, 4, 1, brow)
    c.rect(f, 3, 9, 3, 2, WHITE); c.rect(f, 10, 9, 3, 2, WHITE)
    c.rect(f, 4, 9, 2, 2, (0.22, 0.13, 0.08)); c.rect(f, 10, 9, 2, 2, (0.22, 0.13, 0.08)); c.px(f, 5, 10, BLACK); c.px(f, 10, 10, BLACK)
    c.rect(f, 7, 6, 2, 3, mul(skin, 0.9)); c.rect(f, 7, 6, 2, 1, mul(skin, 0.75))
    if spec.get('wrinkles'): c.px(f, 2, 8, mul(skin, 0.8)); c.px(f, 13, 8, mul(skin, 0.8)); c.rect(f, 4, 12, 8, 1, mul(skin, 0.85))
    beard = spec.get('beard')
    if beard == 'long':
        bc = spec.get('beard_col', WHITE)
        c.rect(f, 3, 4, 10, 2, bc); c.rect(f, 2, 0, 12, 4, bc); c.rect(f, 6, 3, 4, 1, (0.55, 0.30, 0.25))
    elif beard == 'moustache':
        c.rect(f, 4, 4, 8, 2, hair); c.rect(f, 3, 1, 1, 4, hair); c.rect(f, 12, 1, 1, 4, hair); c.rect(f, 6, 3, 4, 1, (0.55, 0.28, 0.24))
    else:
        c.rect(f, 6, 3, 4, 1, spec.get('lips', (0.75, 0.35, 0.35)))
        if spec.get('rosy'): c.px(f, 3, 6, (0.95, 0.55, 0.50)); c.px(f, 12, 6, (0.95, 0.55, 0.50))
    # head back / side
    hb = T['HeadBack']; c.fill(hb, spec.get('frame', hair) if spec.get('frame') else (skin if spec.get('bald') else hair), 0.03)
    if spec.get('braids'):
        for x in (4, 11):
            for y in range(0, 14): c.px(hb, x, y, mul(hair, 1.3) if y % 3 == 0 else hair)
    hs = T['HeadSide']; c.fill(hs, skin, 0.025)
    if spec.get('frame'): c.fill(hs, spec['frame'], 0.03)
    elif not spec.get('bald'): c.rect(hs, 0, 11, 16, 5, hair); c.rect(hs, 0, 4, 5, 7, hair)
    if beard == 'long': c.rect(hs, 6, 0, 10, 6, spec.get('beard_col', WHITE))
    # hat
    hat = spec.get('hat_col', (0.92, 0.90, 0.84)); band = spec.get('hat_band', BLACK)
    c.fill(T['HatSide'], hat, 0.04); c.rect(T['HatSide'], 0, 0, 16, spec.get('band_h', 4), band)
    if spec.get('hat_orn'):
        for x in range(1, 16, 5): koshkar(c, T['HatSide'], x + 1, 8, spec['hat_orn'])
    if spec.get('fur'):
        for x in range(16):
            for y in range(spec.get('band_h', 4)):
                if hsh(x, y, 9) > 0: c.px(T['HatSide'], x, y, mul(band, 1.25))
    c.fill(T['HatTop'], spec.get('hat_top', hat), 0.04)
    if spec.get('top_orn'): c.diamond(T['HatTop'], 8, 8, 4, spec['top_orn']); c.diamond(T['HatTop'], 8, 8, 2, spec['top_orn'])
    # torso
    robe = spec['robe']; trim = spec.get('trim', GOLD); sash = spec.get('sash', RED)
    tf = T['TorsoFront']; c.fill(tf, robe, 0.04)
    if spec.get('shirt'): c.rect(tf, 5, 0, 6, 16, spec['shirt'])
    if spec.get('vest'):
        c.rect(tf, 0, 0, 5, 16, spec['vest']); c.rect(tf, 11, 0, 5, 16, spec['vest'])
        for y in range(1, 16, 3): c.px(tf, 4, y, trim); c.px(tf, 11, y, trim)
    elif not spec.get('apron'):
        for y in range(16): c.px(tf, 7, y, trim); c.px(tf, 8, y, trim)
    if spec.get('apron'): c.rect(tf, 2, 0, 12, 12, spec['apron']); c.rect(tf, 4, 12, 8, 4, spec['apron'])
    if spec.get('mail'):
        for x in range(16):
            for y in range(16):
                if (x + y) % 2 == 0: c.px(tf, x, y, mul(robe, 0.8))
    c.rect(tf, 0, 15, 16, 1, trim)
    if sash: c.rect(tf, 0, 4, 16, 3, sash); c.rect(tf, 6, 3, 4, 5, GOLD) if spec.get('buckle', True) else None
    if spec.get('chest_orn'): koshkar(c, tf, 3, 11, spec['chest_orn']); koshkar(c, tf, 12, 11, spec['chest_orn'])
    ts = T['TorsoSide']; c.fill(ts, robe, 0.04)
    if spec.get('vest'): c.rect(ts, 0, 0, 16, 16, spec['vest'])
    if sash: c.rect(ts, 0, 4, 16, 3, sash)
    if spec.get('mail'):
        for x in range(16):
            for y in range(16):
                if (x + y) % 2 == 0: c.px(ts, x, y, mul(robe, 0.8))
    tb = T['TorsoBack']; c.fill(tb, spec.get('cape', robe), 0.04)
    if spec.get('vest') and not spec.get('cape'): c.fill(tb, spec['vest'], 0.04)
    if sash and not spec.get('cape'): c.rect(tb, 0, 4, 16, 3, sash)
    koshkar(c, tb, 8, 11, trim)
    sl = T['Sleeve']; c.fill(sl, spec.get('sleeve', robe), 0.04); c.rect(sl, 0, 0, 16, 3, trim)
    if spec.get('bare_arms'): c.fill(sl, skin, 0.03)
    c.fill(T['Trouser'], spec.get('trouser', (0.30, 0.22, 0.17)), 0.05)
    bt = T['Boot']; boot = spec.get('boot', (0.16, 0.10, 0.07)); c.fill(bt, boot, 0.04); c.rect(bt, 0, 13, 16, 3, mul(boot, 1.6)); c.rect(bt, 0, 0, 16, 2, BLACK)
    c.fill(T['Sole'], BLACK, 0.02)
    sk = T['Skirt']; c.fill(sk, spec.get('skirt', robe), 0.04); c.rect(sk, 0, 0, 16, 3, trim)
    if spec.get('skirt_orn'):
        for x in range(0, 16, 4): c.diamond(sk, x + 2, 6, 2, spec['skirt_orn'])
    c.fill(T['Prop'], spec.get('prop_col', (0.50, 0.33, 0.17)), 0.05)
    return c


def build_human(name, spec, coll):
    for n in (name, 'SM_' + name):
        o = bpy.data.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    c = paint(spec)
    img = c.to_image('T_' + name); mat = K['make_mat']('M_' + name, img)
    Sx = spec.get('scale', 0.9)
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap'); dv = bm.verts.layers.deform.verify()
    G = ['Leg.L', 'Leg.R', 'Spine', 'Arm.L', 'Arm.R', 'Head']; gi = {g: i for i, g in enumerate(G)}
    T = TILE
    def box(g, ctr, size, tiles): K['add_box'](bm, uv, tuple(v * Sx for v in ctr), tuple(v * Sx for v in size), tiles, dv, gi[g])
    def tl(top, bottom, front, back, left, right): return {'+Z': top, '-Z': bottom, '-Y': front, '+Y': back, '+X': left, '-X': right}
    A = K['all_t']
    for side, x in (('L', 0.12), ('R', -0.12)):
        box('Leg.' + side, (x, 0, 0.42), (0.22, 0.24, 0.36), A(T['Trouser']))
        box('Leg.' + side, (x, -0.02, 0.12), (0.24, 0.30, 0.24), tl(T['Boot'], T['Sole'], T['Boot'], T['Boot'], T['Boot'], T['Boot']))
    skirt_len = spec.get('skirt_len', 0.30)
    box('Spine', (0, 0, 0.70 - skirt_len / 2), (0.54, 0.32, skirt_len), tl(T['TorsoSide'], T['TorsoSide'], T['Skirt'], T['Skirt'], T['Skirt'], T['Skirt']))
    box('Spine', (0, 0, 0.93), (0.50, 0.28, 0.66), tl(T['TorsoSide'], T['TorsoSide'], T['TorsoFront'], T['TorsoBack'], T['TorsoSide'], T['TorsoSide']))
    for side, x in (('L', 0.33), ('R', -0.33)):
        box('Arm.' + side, (x, 0, 0.98), (0.16, 0.18, 0.48), A(T['Sleeve']))
        box('Arm.' + side, (x, 0, 0.68), (0.13, 0.13, 0.12), A(T['Hand']))
    box('Head', (0, 0, 1.45), (0.40, 0.40, 0.40), tl(T['HeadBack'], T['Skin'], T['Face'], T['HeadBack'], T['HeadSide'], T['HeadSide']))
    hat = spec.get('hat', 'kalpak')
    if hat == 'kalpak':
        box('Head', (0, 0, 1.74), (0.45, 0.45, 0.20), tl(T['HatTop'], T['HatTop'], T['HatSide'], T['HatSide'], T['HatSide'], T['HatSide']))
        box('Head', (0, 0, 1.91), (0.32, 0.32, 0.14), A(T['HatTop']))
    elif hat == 'borik':      # fur-rimmed round hat
        box('Head', (0, 0, 1.72), (0.48, 0.48, 0.16), tl(T['HatTop'], T['HatTop'], T['HatSide'], T['HatSide'], T['HatSide'], T['HatSide']))
        box('Head', (0, 0, 1.84), (0.36, 0.36, 0.12), A(T['HatTop']))
    elif hat == 'takiya':
        box('Head', (0, 0, 1.69), (0.42, 0.42, 0.10), tl(T['HatTop'], T['HatTop'], T['HatSide'], T['HatSide'], T['HatSide'], T['HatSide']))
    elif hat == 'tymak':      # ear-flap fur hat
        box('Head', (0, 0, 1.74), (0.46, 0.46, 0.20), tl(T['HatTop'], T['HatTop'], T['HatSide'], T['HatSide'], T['HatSide'], T['HatSide']))
        for x in (0.22, -0.22): box('Head', (x, 0.03, 1.48), (0.06, 0.30, 0.34), A(T['HatSide']))
        box('Head', (0, 0.22, 1.50), (0.44, 0.06, 0.36), A(T['HatSide']))
    elif hat == 'kimeshek':   # white headdress + turban-like top
        box('Head', (0, 0.02, 1.47), (0.44, 0.44, 0.46), tl(T['HatTop'], T['HatTop'], T['Face'], T['HeadBack'], T['HeadSide'], T['HeadSide']))
        box('Head', (0, 0, 1.73), (0.46, 0.46, 0.14), A(T['HatSide']))
        box('Head', (0, 0.08, 1.20), (0.46, 0.40, 0.14), A(T['HeadBack']))
    elif hat == 'duly':       # warrior helmet with spike & aventail
        box('Head', (0, 0, 1.72), (0.46, 0.46, 0.16), tl(T['HatTop'], T['HatTop'], T['HatSide'], T['HatSide'], T['HatSide'], T['HatSide']))
        box('Head', (0, 0, 1.85), (0.30, 0.30, 0.12), A(T['HatTop']))
        box('Head', (0, 0, 1.98), (0.06, 0.06, 0.16), A(T['HatTop']))
        box('Head', (0, 0.06, 1.40), (0.46, 0.36, 0.30), tl(T['HatTop'], T['HatTop'], T['HatTop'], T['HatTop'], T['HatTop'], T['HatTop']))
    if spec.get('owl_feathers'):
        box('Head', (0, 0, 1.80), (0.10, 0.10, 0.12), A(T['HatTop']))
        box('Head', (0, 0, 1.90), (0.14, 0.14, 0.10), A(T['Prop']))
    if spec.get('braid_boxes'):
        for x in (0.10, -0.10): box('Head', (x, 0.22, 1.05), (0.07, 0.07, 0.55), A(T['HeadBack']))
    if spec.get('cape'): box('Spine', (0, 0.17, 0.85), (0.52, 0.05, 0.80), A(T['TorsoBack']))
    prop = spec.get('prop')
    if prop == 'staff':
        box('Arm.R', (-0.33, -0.06, 0.95), (0.05, 0.05, 1.5), A(T['Prop']))
    elif prop == 'crook':
        box('Arm.R', (-0.33, -0.06, 0.95), (0.05, 0.05, 1.5), A(T['Prop'])); box('Arm.R', (-0.33, -0.12, 1.70), (0.05, 0.15, 0.05), A(T['Prop']))
    elif prop == 'hammer':
        box('Arm.R', (-0.33, -0.12, 0.66), (0.04, 0.30, 0.04), A(T['Prop'])); box('Arm.R', (-0.33, -0.28, 0.66), (0.10, 0.08, 0.14), A(T['HatTop']))
    elif prop == 'spear':
        box('Arm.R', (-0.33, -0.06, 1.10), (0.05, 0.05, 2.0), A(T['Prop'])); box('Arm.R', (-0.33, -0.06, 2.16), (0.07, 0.07, 0.22), A(T['HatTop']))
    elif prop == 'pouch':
        box('Spine', (0.22, -0.16, 0.72), (0.12, 0.06, 0.14), A(T['Prop']))
    me_obj = K['mesh_obj']('SM_' + name, bm, mat, coll)
    for g in G: me_obj.vertex_groups.new(name=g)
    arm = bpy.data.armatures.new('ARM_' + name); rig = bpy.data.objects.new(name, arm); coll.objects.link(rig)
    vl = bpy.context.view_layer
    for o in vl.objects: o.select_set(False)
    vl.objects.active = rig; rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); eb = arm.edit_bones
    def bone(n, h, t, p=None):
        b = eb.new(n); b.head = tuple(v * Sx for v in h); b.tail = tuple(v * Sx for v in t); b.roll = 0
        if p: b.parent = eb[p]
    bone('Root', (0, 0, 0), (0, 0.3, 0)); bone('Hips', (0, 0, 0.6), (0, 0, 0.75), 'Root'); bone('Spine', (0, 0, 0.75), (0, 0, 1.25), 'Hips')
    bone('Head', (0, 0, 1.25), (0, 0, 1.7), 'Spine')
    for side, x in (('L', 1), ('R', -1)):
        bone('Leg.' + side, (0.12 * x, 0, 0.6), (0.12 * x, 0, 0.05), 'Hips'); bone('Arm.' + side, (0.33 * x, 0, 1.22), (0.33 * x, 0, 0.65), 'Spine')
    bpy.ops.object.mode_set(mode='OBJECT')
    me_obj.parent = rig; m = me_obj.modifiers.new('Armature', 'ARMATURE'); m.object = rig
    return rig


def human_anims(rig, work='hammer'):
    pb = rig.pose.bones
    for b in pb: b.rotation_mode = 'XYZ'
    if rig.animation_data is None: rig.animation_data_create()
    def reset():
        for b in pb: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
    def act(nm, length, fn):
        full = f"{rig.name}_{nm}"
        old = bpy.data.actions.get(full)
        if old: bpy.data.actions.remove(old)
        a = bpy.data.actions.new(full); a.use_fake_user = True; rig.animation_data.action = a
        for fr in range(0, length + 1, 2):
            reset(); fn(fr / length)
            for b in pb: b.keyframe_insert('rotation_euler', frame=fr + 1); b.keyframe_insert('location', frame=fr + 1)
        a.frame_range = (1, length + 1)
    # sign conventions (verified on the player rig): legs/arms negative X = forward; spine/head positive X = lean/nod forward
    def idle(t):
        s = S(t * TAU); pb['Arm.L'].rotation_euler.z = R(4 + 1.5 * s); pb['Arm.R'].rotation_euler.z = -R(4 + 1.5 * s)
        pb['Spine'].rotation_euler.x = R(1.5) * s; pb['Head'].rotation_euler.z = R(10) * S(t * TAU * 0.5)
    def walk(t):
        s = S(t * TAU); pb['Leg.L'].rotation_euler.x = -R(30) * s; pb['Leg.R'].rotation_euler.x = R(30) * s
        pb['Arm.L'].rotation_euler.x = R(25) * s; pb['Arm.R'].rotation_euler.x = -R(25) * s
        pb['Hips'].location.y = 0.025 * abs(math.cos(t * TAU))
    def talk(t):
        s = S(t * TAU * 2); pb['Arm.R'].rotation_euler.x = -R(40 + 15 * s); pb['Arm.R'].rotation_euler.z = -R(10)
        pb['Arm.L'].rotation_euler.x = -R(15 + 8 * S(t * TAU)); pb['Head'].rotation_euler.x = R(5) * S(t * TAU * 3)
        pb['Head'].rotation_euler.z = R(8) * S(t * TAU)
    def work_fn(t):
        if work == 'hammer':
            a = S(t * TAU); pb['Arm.R'].rotation_euler.x = -R(70 + 50 * a); pb['Spine'].rotation_euler.x = R(10 + 6 * a)
            pb['Arm.L'].rotation_euler.x = -R(40)
        else:   # sweeping / shepherd's gesture
            pb['Arm.R'].rotation_euler.x = -R(35 + 20 * S(t * TAU)); pb['Arm.L'].rotation_euler.x = -R(35 + 20 * S(t * TAU))
            pb['Spine'].rotation_euler.x = R(15); pb['Spine'].rotation_euler.z = R(12) * S(t * TAU)
    act('Idle', 48, idle); act('Walk', 24, walk); act('Talk', 48, talk); act('Work', 24, work_fn)
    reset(); rig.animation_data.action = bpy.data.actions[f"{rig.name}_Idle"]


NPCS = {
    'Aksakal': dict(hat='borik', hat_col=(0.75, 0.12, 0.10), hat_band=(0.40, 0.28, 0.18), fur=True, band_h=6, hat_top=(0.75, 0.12, 0.10), top_orn=GOLD,
                    hair=(0.85, 0.85, 0.82), brow=(0.90, 0.90, 0.88), beard='long', beard_col=(0.96, 0.96, 0.94), wrinkles=True,
                    robe=(0.36, 0.24, 0.14), trim=GOLD, sash=(0.20, 0.35, 0.20), skirt_len=0.55, prop='staff', prop_col=(0.45, 0.30, 0.15)),
    'Apa': dict(hat='kimeshek', frame=(0.97, 0.96, 0.93), hat_col=(0.97, 0.96, 0.93), hat_band=(0.97, 0.96, 0.93), hat_top=(0.97, 0.96, 0.93), hat_orn=RED,
                wrinkles=True, robe=(0.55, 0.10, 0.12), vest=(0.15, 0.20, 0.45), trim=GOLD, sash=None, skirt=(0.55, 0.10, 0.12), skirt_len=0.6, skirt_orn=GOLD,
                lips=(0.70, 0.40, 0.38)),
    'Kyz': dict(hat='takiya', hat_col=(0.70, 0.10, 0.15), hat_band=GOLD, band_h=2, hat_orn=GOLD, hat_top=(0.70, 0.10, 0.15), owl_feathers=True,
                braids=True, braid_boxes=True, robe=(0.85, 0.20, 0.25), vest=(0.10, 0.45, 0.30), trim=GOLD, sash=None, shirt=WHITE,
                skirt=(0.85, 0.20, 0.25), skirt_len=0.5, skirt_orn=WHITE, rosy=True, scale=0.75, prop_col=(0.95, 0.95, 0.95)),
    'Bala': dict(hat='takiya', hat_col=(0.12, 0.12, 0.14), hat_band=WHITE, band_h=2, hat_orn=WHITE, robe=WHITE, vest=(0.15, 0.30, 0.65), trim=GOLD,
                 sash=None, trouser=(0.20, 0.20, 0.25), rosy=True, scale=0.68),
    'Merchant': dict(hat='borik', hat_col=(0.40, 0.12, 0.45), hat_band=(0.30, 0.22, 0.15), fur=True, band_h=6, hat_top=(0.40, 0.12, 0.45), top_orn=GOLD,
                     beard='moustache', robe=(0.42, 0.15, 0.48), trim=GOLD, sash=(0.95, 0.75, 0.25), chest_orn=GOLD, skirt_len=0.5, prop='pouch', prop_col=(0.55, 0.35, 0.18)),
    'Blacksmith': dict(hat=None, bald=True, beard='moustache', robe=(0.55, 0.42, 0.30), apron=(0.35, 0.22, 0.12), bare_arms=True, trim=(0.25, 0.15, 0.08),
                       sash=None, buckle=False, prop='hammer', prop_col=(0.40, 0.28, 0.15), hat_top=(0.30, 0.30, 0.33)),
    'Batyr': dict(hat='duly', hat_col=(0.62, 0.62, 0.66), hat_band=GOLD, band_h=2, hat_top=(0.70, 0.70, 0.74), beard='moustache',
                  robe=(0.55, 0.56, 0.60), mail=True, trim=GOLD, sash=RED, cape=(0.65, 0.10, 0.10), sleeve=(0.50, 0.50, 0.55), prop='spear', prop_col=(0.40, 0.26, 0.14)),
    'Shepherd': dict(hat='tymak', hat_col=(0.55, 0.38, 0.22), hat_band=(0.80, 0.72, 0.58), fur=True, band_h=5, hat_top=(0.20, 0.30, 0.55),
                     beard='moustache', robe=(0.82, 0.74, 0.58), trim=(0.55, 0.40, 0.25), sash=(0.30, 0.22, 0.15), skirt_len=0.5, prop='crook', prop_col=(0.50, 0.35, 0.20)),
}
coll = get_coll('NPC')
NPC_DONE = []
for i, (n, spec) in enumerate(NPCS.items()):
    rig = build_human(n, spec, coll)
    human_anims(rig, 'hammer' if n == 'Blacksmith' else 'gesture')
    rig.location = (-30 + i * 1.6, -80, 0)
    NPC_DONE.append(n)
