"""DalaCraft — hostile & neutral mobs, Minecraft-style cuboid models with calm 16px textures, rigged and animated:
  Kanka (skeleton archer in a rusty дулыға), Olik (zombie in a torn chapan), Albasty (floating spirit with long hair),
  Ayu (taiga bear), Karakurt (black spider with red spots), Shaitan (horned fire demon of the desert).
Exports Resources/Models/Mobs/<Name>.fbx with Idle / Walk / Run / Attack takes (+ Hurt-free; the game flashes red).
Run after km_tools.py and mc_models.py:  exec(open(".../Blender/tools/mobs.py").read())
"""
import bpy, math
import numpy as np
M = bpy.app.driver_namespace['mc']; K = bpy.app.driver_namespace['km']
Model, solid, layered, stripes, border, mul, pixels = M['Model'], M['solid'], M['layered'], M['stripes'], M['border'], M['mul'], M['pixels']
R = math.radians; S = math.sin; TAU = 2 * math.pi
PX = 1 / 16
DONE = []


def patch(col, x, y, w, h, top_origin=True):
    """a coloured rectangle on a face (x from the left, y from the top if top_origin)"""
    def p(W, H, s):
        a = np.zeros((H, W, 4), np.float32)
        y0 = H - y - h if top_origin else y
        a[max(0, y0):max(0, y0 + h), max(0, x):x + w] = (*col, 1)
        return a
    return p


def spots(col, n, seed, size=1):
    def p(W, H, s):
        a = np.zeros((H, W, 4), np.float32)
        for i in range(n):
            x = int((K['hsh'](i, 1, seed) + 0.5) * (W - size)); y = int((K['hsh'](i, 2, seed) + 0.5) * (H - size))
            a[y:y + size, x:x + size] = (*col, 1)
        return a
    return p


def rig(name, model, bones, coll):
    """bones: (name, head px, tail px, parent); boxes were tagged with these names as groups"""
    for n in (name, 'SM_' + name):
        o = bpy.data.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    groups = [b[0] for b in bones]
    mesh = model.build(coll, groups=groups)
    arm = bpy.data.armatures.get('ARM_' + name)
    if arm: bpy.data.armatures.remove(arm)
    arm = bpy.data.armatures.new('ARM_' + name); r = bpy.data.objects.new(name, arm); coll.objects.link(r)
    vl = bpy.context.view_layer
    for o in vl.objects: o.select_set(False)
    vl.objects.active = r; r.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for (bn, h, t, p) in bones:
        b = arm.edit_bones.new(bn); b.head = tuple(v * PX for v in h); b.tail = tuple(v * PX for v in t); b.roll = 0
        if p: b.parent = arm.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    mesh.parent = r
    m = mesh.modifiers.new('Armature', 'ARMATURE'); m.object = r
    return r, mesh


def anims(r, clips):
    """clips: name → (frames, fn(t, pose_bones)); every bone keyed every 2 frames"""
    pb = r.pose.bones
    for b in pb: b.rotation_mode = 'XYZ'
    if r.animation_data is None: r.animation_data_create()
    names = []
    for nm, (length, fn) in clips.items():
        full = f"{r.name}_{nm}"
        old = bpy.data.actions.get(full)
        if old: bpy.data.actions.remove(old)
        a = bpy.data.actions.new(full); a.use_fake_user = True; r.animation_data.action = a
        for fr in range(0, length + 1, 2):
            for b in pb: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
            fn(fr / length, pb)
            for b in pb: b.keyframe_insert('rotation_euler', frame=fr + 1); b.keyframe_insert('location', frame=fr + 1)
        a.frame_range = (1, length + 1)
        names.append(full)
    for b in pb: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
    r.animation_data.action = bpy.data.actions[names[0]]
    return names


def export(name, r, mesh, actions):
    K['export']([r, mesh], f"{K['RM']}/Mobs/{name}.fbx", r, actions)
    DONE.append(name)


coll = K['get_coll']('Mobs')
HUMAN_BONES = lambda leg_x, arm_x, hip, shoulder, neck: [
    ('Root', (0, 0, 0), (0, 4, 0), None), ('Hips', (0, 0, hip), (0, 0, hip + 2), 'Root'), ('Spine', (0, 0, hip + 2), (0, 0, neck), 'Hips'),
    ('Head', (0, 0, neck), (0, 0, neck + 6), 'Spine'),
    ('Leg.L', (leg_x, 0, hip), (leg_x, 0, 1), 'Hips'), ('Leg.R', (-leg_x, 0, hip), (-leg_x, 0, 1), 'Hips'),
    ('Arm.L', (arm_x, 0, shoulder), (arm_x, 0, shoulder - 10), 'Spine'), ('Arm.R', (-arm_x, 0, shoulder), (-arm_x, 0, shoulder - 10), 'Spine')]


def human_clips(walk_arms=0.0, attack='swing', run_speed=1.0):
    """sign conventions from the NPC rigs: legs/arms negative X = forward; spine/head positive X = lean forward"""
    def idle(t, pb):
        s = S(t * TAU); pb['Arm.L'].rotation_euler.z = R(3 + s); pb['Arm.R'].rotation_euler.z = -R(3 + s)
        pb['Arm.L'].rotation_euler.x = pb['Arm.R'].rotation_euler.x = -R(walk_arms)
        pb['Head'].rotation_euler.z = R(8) * S(t * TAU * 0.5); pb['Spine'].rotation_euler.x = R(1.5) * s
    def walk(t, pb, amp=30):
        s = S(t * TAU); pb['Leg.L'].rotation_euler.x = -R(amp) * s; pb['Leg.R'].rotation_euler.x = R(amp) * s
        if walk_arms: pb['Arm.L'].rotation_euler.x = -R(walk_arms + 6 * s); pb['Arm.R'].rotation_euler.x = -R(walk_arms - 6 * s)
        else: pb['Arm.L'].rotation_euler.x = R(amp * 0.8) * s; pb['Arm.R'].rotation_euler.x = -R(amp * 0.8) * s
        pb['Hips'].location.y = 0.02 * abs(math.cos(t * TAU))
    def run(t, pb):
        walk(t, pb, 45); pb['Spine'].rotation_euler.x = R(10)
    def att(t, pb):
        k = S(min(1, t * 1.6) * math.pi)
        if attack == 'bow':      # draw the bow: both arms up and forward, then release
            pb['Arm.L'].rotation_euler.x = -R(85); pb['Arm.R'].rotation_euler.x = -R(80 - 25 * k); pb['Arm.R'].rotation_euler.z = -R(20 * k)
            pb['Spine'].rotation_euler.z = R(10)
        else:                    # overhead swing with the right arm (both for zombies)
            pb['Arm.R'].rotation_euler.x = -R(walk_arms + 60 * k); pb['Arm.L'].rotation_euler.x = -R(walk_arms + (40 * k if walk_arms else 0))
            pb['Spine'].rotation_euler.x = R(12 * k)
    return {'Idle': (48, idle), 'Walk': (24, walk), 'Run': (16, run), 'Attack': (16, att)}


# ================================================================ Kanka — skeleton archer
BONE, BONE_D, GAP = (0.88, 0.86, 0.80), (0.62, 0.60, 0.54), (0.16, 0.13, 0.13)
RUST, RED = (0.50, 0.30, 0.18), (0.62, 0.14, 0.11)
bone = solid(BONE, 0.03)
m = Model('SM_Kanka')
for x, g in ((1, 'Leg.L'), (-3, 'Leg.R')):
    m.box((x, -1, 0), (x + 2, 1, 12), {'all': layered(bone, stripes([None] * 5 + [BONE_D] + [None] * 6))}, group=g)
m.box((-4, -2, 12), (4, 2, 24), {'front': layered(solid(GAP), stripes([None, BONE, None, BONE, None, BONE, None, BONE, None, BONE, BONE, BONE]), patch(BONE, 3, 0, 2, 12)),
                                  'back': layered(solid(GAP), patch(BONE, 3, 0, 2, 12)), 'side': layered(solid(GAP), stripes([None, BONE] * 6)), 'top': bone, 'bottom': bone}, group='Spine')
m.box((-4.2, -2.2, 13), (4.2, 2.2, 15), {'all': solid(RED, 0.03)}, group='Spine')                        # tattered sash
for x, g in ((4, 'Arm.L'), (-6, 'Arm.R')):
    m.box((x, -1, 12), (x + 2, 1, 24), {'all': layered(bone, stripes([None] * 5 + [BONE_D] + [None] * 6))}, group=g)
skull = layered(solid(BONE, 0.03), patch(GAP, 1, 3, 2, 2), patch(GAP, 5, 3, 2, 2), patch(GAP, 3, 5, 2, 1), patch(BONE_D, 1, 7, 6, 1))
m.box((-4, -4, 24), (4, 4, 32), {'front': skull, 'side': bone, 'back': bone, 'top': bone, 'bottom': bone}, group='Head')
m.box((-4.5, -4.5, 30), (4.5, 4.5, 33), {'side': layered(solid(RUST, 0.06), stripes([mul(RUST, 0.7)])), 'top': solid(RUST, 0.06)}, group='Head')   # дулыға
m.box((-0.5, -0.5, 33), (0.5, 0.5, 36), {'all': solid(RUST)}, group='Head')
m.box((-6, -3, 13), (-5, 3, 23), {'all': solid((0.45, 0.30, 0.15))}, group='Arm.R')                      # bow
r, mesh = rig('Kanka', m, HUMAN_BONES(2, 5, 12, 23, 24), coll)
export('Kanka', r, mesh, anims(r, human_clips(attack='bow')))

# ================================================================ Olik — zombie in a torn chapan
SKIN, CHAPAN, TROUS = (0.44, 0.54, 0.40), (0.30, 0.34, 0.42), (0.22, 0.20, 0.18)
m = Model('SM_Olik')
for x, g in ((0, 'Leg.L'), (-4, 'Leg.R')):
    m.box((x, -2, 0), (x + 4, 2, 12), {'all': layered(solid(TROUS, 0.04), stripes([(0.12, 0.10, 0.09)] * 2))}, group=g)
torn = layered(solid(CHAPAN, 0.04), stripes([mul(CHAPAN, 0.75), None, None, mul(CHAPAN, 0.75)]), spots(SKIN, 4, 11, 2))
m.box((-4, -2, 12), (4, 2, 24), {'side': torn, 'top': solid(CHAPAN), 'bottom': solid(CHAPAN)}, group='Spine')
for x, g in ((4, 'Arm.L'), (-8, 'Arm.R')):
    m.box((x, -2, 12), (x + 4, 2, 24), {'all': layered(solid(CHAPAN, 0.04), stripes([SKIN] * 3 + [None] * 9), spots(SKIN, 2, 12 + x, 1))}, group=g)
face = layered(solid(SKIN, 0.04), patch((0.35, 0.05, 0.05), 1, 3, 2, 1), patch((0.35, 0.05, 0.05), 5, 3, 2, 1), patch(mul(SKIN, 0.7), 2, 6, 4, 1))
m.box((-4, -4, 24), (4, 4, 32), {'front': face, 'side': solid(SKIN, 0.04), 'back': solid(SKIN, 0.04), 'top': solid(mul(SKIN, 0.85), 0.05), 'bottom': solid(SKIN)}, group='Head')
r, mesh = rig('Olik', m, HUMAN_BONES(2, 6, 12, 23, 24), coll)
export('Olik', r, mesh, anims(r, human_clips(walk_arms=80)))

# ================================================================ Albasty — floating spirit, long black hair, pale robe
PALE, ROBE, HAIR, GLOW = (0.86, 0.90, 0.94), (0.80, 0.86, 0.90), (0.08, 0.08, 0.10), (0.55, 1.0, 1.0)
m = Model('SM_Albasty')
m.box((-5, -3, 6), (5, 3, 12), {'side': layered(solid(ROBE, 0.03), stripes([mul(ROBE, 0.85)] * 2)), 'bottom': solid(mul(ROBE, 0.8)), 'top': solid(ROBE)}, group='Spine')
m.box((-3.5, -2, 2), (3.5, 2, 6), {'side': solid(mul(ROBE, 0.92), 0.03), 'bottom': solid(mul(ROBE, 0.7))}, group='Spine')    # robe tail (no legs)
m.box((-4, -2, 12), (4, 2, 22), {'side': solid(ROBE, 0.03), 'top': solid(ROBE)}, group='Spine')
for x, g in ((4, 'Arm.L'), (-6, 'Arm.R')):
    m.box((x, -1, 10), (x + 2, 1, 22), {'all': layered(solid(PALE, 0.03), stripes([mul(PALE, 0.7)] * 2))}, group=g)
face = layered(solid(PALE, 0.02), patch(GLOW, 1, 3, 2, 1), patch(GLOW, 5, 3, 2, 1), patch(HAIR, 0, 0, 8, 2))
m.box((-4, -4, 22), (4, 4, 30), {'front': face, 'side': solid(HAIR, 0.04), 'back': solid(HAIR, 0.04), 'top': solid(HAIR, 0.04), 'bottom': solid(PALE)}, group='Head')
m.box((-4.5, 0, 10), (4.5, 4.5, 30), {'all': layered(solid(HAIR, 0.04), stripes([None, (0.16, 0.16, 0.2)] * 10, horizontal=False))}, group='Head')   # hair to the waist
bones = [('Root', (0, 0, 0), (0, 4, 0), None), ('Hips', (0, 0, 8), (0, 0, 12), 'Root'), ('Spine', (0, 0, 12), (0, 0, 21), 'Hips'),
         ('Head', (0, 0, 21), (0, 0, 28), 'Spine'), ('Arm.L', (5, 0, 21), (5, 0, 11), 'Spine'), ('Arm.R', (-5, 0, 21), (-5, 0, 11), 'Spine')]
r, mesh = rig('Albasty', m, bones, coll)
def a_idle(t, pb):
    pb['Hips'].location.z = 0.06 * S(t * TAU)          # bobbing in the air
    pb['Arm.L'].rotation_euler.x = -R(20 + 8 * S(t * TAU)); pb['Arm.R'].rotation_euler.x = -R(20 + 8 * S(t * TAU + 1))
    pb['Head'].rotation_euler.z = R(14) * S(t * TAU * 0.5)
def a_fly(t, pb):
    pb['Hips'].location.z = 0.05 * S(t * TAU * 2); pb['Spine'].rotation_euler.x = R(25)
    pb['Arm.L'].rotation_euler.x = R(30); pb['Arm.R'].rotation_euler.x = R(30)
def a_att(t, pb):
    k = S(min(1, t * 1.5) * math.pi)
    pb['Arm.L'].rotation_euler.x = -R(90 * k); pb['Arm.R'].rotation_euler.x = -R(90 * k); pb['Spine'].rotation_euler.x = R(20 * k)
export('Albasty', r, mesh, anims(r, {'Idle': (48, a_idle), 'Walk': (24, a_fly), 'Run': (16, a_fly), 'Attack': (16, a_att)}))

# ================================================================ Ayu — taiga bear
FUR, FUR_D, SNOUT = (0.40, 0.27, 0.16), (0.30, 0.20, 0.12), (0.62, 0.48, 0.34)
fur = solid(FUR, 0.05)
m = Model('SM_Ayu')
for (x, y, g) in ((2, -10, 'Leg.FL'), (-7, -10, 'Leg.FR'), (2, 5, 'Leg.BL'), (-7, 5, 'Leg.BR')):
    m.box((x, y, 0), (x + 5, y + 5, 10), {'all': layered(solid(FUR_D, 0.05), stripes([mul(FUR_D, 0.6)]))}, group=g)
m.box((-7, -12, 9), (7, 12, 22), {'all': fur, 'top': solid(mul(FUR, 1.08), 0.05)}, group='Body')
m.box((-5, -20, 13), (5, -12, 22), {'front': layered(fur, patch((0.05, 0.05, 0.05), 2, 3, 1, 1), patch((0.05, 0.05, 0.05), 7, 3, 1, 1)), 'side': fur, 'back': fur, 'top': fur, 'bottom': fur}, group='Head')
m.box((-2.5, -24, 13), (2.5, -20, 17), {'front': layered(solid(SNOUT), patch((0.05, 0.05, 0.05), 1, 0, 3, 1)), 'side': solid(SNOUT), 'top': solid(SNOUT), 'bottom': solid(SNOUT)}, group='Head')
for x in (-5, 3): m.box((x, -15, 22), (x + 2, -14, 24), {'all': solid(FUR_D)}, group='Head')
bones = [('Root', (0, 0, 0), (0, 4, 0), None), ('Body', (0, 0, 15), (0, -8, 15), 'Root'), ('Head', (0, -12, 17), (0, -20, 17), 'Body'),
         ('Leg.FL', (4.5, -7.5, 10), (4.5, -7.5, 1), 'Body'), ('Leg.FR', (-4.5, -7.5, 10), (-4.5, -7.5, 1), 'Body'),
         ('Leg.BL', (4.5, 7.5, 10), (4.5, 7.5, 1), 'Body'), ('Leg.BR', (-4.5, 7.5, 10), (-4.5, 7.5, 1), 'Body')]
r, mesh = rig('Ayu', m, bones, coll)
def q_walk(amp):
    def f(t, pb):
        s = S(t * TAU)
        pb['Leg.FL'].rotation_euler.x = R(amp) * s; pb['Leg.BR'].rotation_euler.x = R(amp) * s
        pb['Leg.FR'].rotation_euler.x = -R(amp) * s; pb['Leg.BL'].rotation_euler.x = -R(amp) * s
        pb['Head'].rotation_euler.x = R(4) * S(t * TAU * 2)
    return f
def b_idle(t, pb): pb['Head'].rotation_euler.z = R(12) * S(t * TAU); pb['Body'].location.z = 0.01 * S(t * TAU)
def b_att(t, pb):
    k = S(min(1, t * 1.4) * math.pi)          # rears up and swipes
    pb['Body'].rotation_euler.x = -R(35 * k); pb['Leg.FL'].rotation_euler.x = -R(50 * k); pb['Leg.FR'].rotation_euler.x = -R(30 * k)
    pb['Head'].rotation_euler.x = R(20 * k)
export('Ayu', r, mesh, anims(r, {'Idle': (48, b_idle), 'Walk': (24, q_walk(22)), 'Run': (14, q_walk(38)), 'Attack': (20, b_att)}))

# ================================================================ Karakurt — black spider with red spots
BLACK, RED2, EYE = (0.08, 0.07, 0.08), (0.80, 0.08, 0.08), (0.95, 0.15, 0.10)
m = Model('SM_Karakurt')
m.box((-6, 0, 3), (6, 13, 13), {'all': layered(solid(BLACK, 0.04), spots(RED2, 6, 41, 2))}, group='Body')               # abdomen
m.box((-4, -7, 4), (4, 0, 10), {'all': solid(mul(BLACK, 1.4), 0.04)}, group='Body')                                       # cephalothorax
m.box((-3, -11, 5), (3, -7, 9), {'front': layered(solid(BLACK, 0.04), patch(EYE, 1, 1, 1, 1), patch(EYE, 4, 1, 1, 1), patch(EYE, 2, 0, 2, 1)),
                                  'side': solid(BLACK), 'top': solid(BLACK), 'bottom': solid(BLACK)}, group='Head')
leg = {'all': layered(solid((0.12, 0.11, 0.12), 0.04), stripes([None] * 6 + [(0.35, 0.05, 0.05)]))}
bones = [('Root', (0, 0, 0), (0, 4, 0), None), ('Body', (0, -2, 7), (0, 6, 7), 'Root'), ('Head', (0, -7, 7), (0, -11, 7), 'Body')]
for i, y in enumerate((-6, -3, 0, 3)):
    for side, sx in (('L', 1), ('R', -1)):
        g = f'Leg.{side}{i + 1}'
        x0 = 3 * sx
        m.box((min(x0, x0 + 15 * sx), y - 0.75, 6.25), (max(x0, x0 + 15 * sx), y + 0.75, 7.75), leg, group=g, rot=('y', 26 * sx, (x0, y, 7)))
        bones.append((g, (x0, y, 7), (x0, y, 1), 'Body'))
r, mesh = rig('Karakurt', m, bones, coll)
def s_walk(amp):
    def f(t, pb):
        for i in range(4):
            ph = t * TAU + i * math.pi / 2
            pb[f'Leg.L{i + 1}'].rotation_euler.y = R(amp) * S(ph); pb[f'Leg.R{i + 1}'].rotation_euler.y = R(amp) * S(ph + math.pi)
        pb['Body'].location.z = 0.01 * S(t * TAU * 4)
    return f
def s_idle(t, pb):
    for i in range(4): pb[f'Leg.L{i + 1}'].rotation_euler.y = R(3) * S(t * TAU + i); pb[f'Leg.R{i + 1}'].rotation_euler.y = R(3) * S(t * TAU + i + 2)
def s_att(t, pb):
    k = S(min(1, t * 1.5) * math.pi)
    for side in 'LR': pb[f'Leg.{side}1'].rotation_euler.x = -R(40 * k)
    pb['Body'].rotation_euler.x = -R(15 * k)
export('Karakurt', r, mesh, anims(r, {'Idle': (40, s_idle), 'Walk': (16, s_walk(25)), 'Run': (10, s_walk(32)), 'Attack': (14, s_att)}))

# ================================================================ Shaitan — horned fire demon
RED_S, FUR_S, HORN, FIRE = (0.60, 0.15, 0.10), (0.20, 0.12, 0.10), (0.85, 0.80, 0.68), (1.0, 0.75, 0.20)
m = Model('SM_Shaitan')
for x, g in ((0.5, 'Leg.L'), (-3.5, 'Leg.R')):
    m.box((x, -1.5, 0), (x + 3, 1.5, 11), {'all': layered(solid(FUR_S, 0.06), stripes([(0.06, 0.05, 0.05)] * 2))}, group=g)      # goat legs, hooves
m.box((-4, -2, 11), (4, 2, 21), {'front': layered(solid(RED_S, 0.04), patch(mul(RED_S, 0.75), 3, 2, 2, 6)), 'side': solid(RED_S, 0.04), 'top': solid(RED_S), 'bottom': solid(FUR_S)}, group='Spine')
for x, g in ((4, 'Arm.L'), (-7, 'Arm.R')):
    m.box((x, -1.5, 11), (x + 3, 1.5, 21), {'all': layered(solid(RED_S, 0.04), stripes([(0.10, 0.06, 0.05)] * 2))}, group=g)       # dark claws
face = layered(solid(RED_S, 0.04), patch(FIRE, 1, 3, 2, 1), patch(FIRE, 5, 3, 2, 1), patch((0.12, 0.05, 0.04), 2, 6, 4, 1))
m.box((-4, -4, 21), (4, 4, 29), {'front': face, 'side': solid(RED_S, 0.04), 'back': solid(RED_S, 0.04), 'top': solid(mul(RED_S, 0.85)), 'bottom': solid(RED_S)}, group='Head')
for sx in (1, -1):
    m.box((2.5 * sx - 1, -1, 28), (2.5 * sx + 1, 1, 33), {'all': layered(solid(HORN, 0.04), stripes([mul(HORN, 0.8), None]))}, group='Head', rot=('y', 20 * sx, (2.5 * sx, 0, 28)))
m.box((-0.5, 2, 12), (0.5, 3, 22), {'all': solid(RED_S)}, group='Spine', rot=('x', -55, (0, 2.5, 12)))      # tail
m.box((-1, 1.5, 22), (1, 3.5, 24), {'all': solid(FIRE)}, group='Spine', rot=('x', -55, (0, 2.5, 12)))      # flaming tail tip
r, mesh = rig('Shaitan', m, HUMAN_BONES(2, 5.5, 11, 20, 21), coll)
export('Shaitan', r, mesh, anims(r, human_clips()))

for i, n in enumerate(DONE): bpy.data.objects[n].location = (-80 + i * 3, -120, 0)
MOBS_DONE = DONE
M['rig'] = rig; M['anims'] = anims; M['patch'] = patch; M['spots'] = spots
