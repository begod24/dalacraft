"""DalaCraft — first-person arm (like Minecraft's): red chapan sleeve with gold-trimmed cuff and a bare hand.
Arm points along Blender -Y (= Unity +Z, forward); origin at the shoulder end. 1/32 m voxels, 8x8 section, 0.75 m long.
Run after km_tools.py:  exec(open(".../Blender/tools/fp_arm.py").read())   →  Unity/Assets/Resources/Models/Player/SM_FP_Arm.fbx
"""
import bpy
K = bpy.app.driver_namespace['km']; hsh, voxel_mesh, get_coll = K['hsh'], K['voxel_mesh'], K['get_coll']

PAL = {'red': (0.72, 0.13, 0.11), 'redD': (0.55, 0.09, 0.08), 'redL': (0.80, 0.20, 0.16),
       'gold': (0.90, 0.70, 0.22), 'goldD': (0.70, 0.50, 0.14), 'black': (0.08, 0.06, 0.05),
       'skin': (0.84, 0.62, 0.45), 'skinD': (0.74, 0.53, 0.38), 'nail': (0.90, 0.72, 0.60)}

v = {}
SLEEVE, CUFF, HAND = 16, 3, 5          # lengths in voxels along the arm
for y in range(SLEEVE + CUFF):
    for x in range(8):
        for z in range(8):
            if 0 < x < 7 and 0 < z < 7: continue           # hollow inside, faces only on the shell
            if y < SLEEVE:
                h = hsh(x, y, z, 3)
                k = 'redL' if h > 0.35 else ('redD' if h < -0.3 else 'red')
                if (y % 6 == 3) and z == 7 and x in (2, 5): k = 'gold'   # small gold buttons on the top seam
            else:
                k = 'gold' if y != SLEEVE + 1 else ('black' if (x + z) % 2 else 'goldD')   # ornamented cuff band
            v[(x, -y, z)] = k
for y in range(SLEEVE + CUFF, SLEEVE + CUFF + HAND):
    for x in range(1, 7):
        for z in range(1, 7):
            k = 'skinD' if (x in (1, 6) or z == 1) else 'skin'
            if y == SLEEVE + CUFF + HAND - 1 and z == 6: k = 'nail'
            v[(x, -y, z)] = k

coll = get_coll('Player')
o = voxel_mesh('SM_FP_Arm', v, 1 / 32, 'T_Palette_FP_Arm', PAL, coll, loc=(4, -2, 1), origin=(4, 0, 4))
K['export']([o], K['RM'] + "/Player/SM_FP_Arm.fbx")
K['save_images']()
FP_ARM_DONE = True
