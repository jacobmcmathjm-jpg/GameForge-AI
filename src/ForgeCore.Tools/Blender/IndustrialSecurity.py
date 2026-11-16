"""Original bounded industrial-security character. Layered procedural art, not photoreal scanned human."""
import bpy
import json
import math
import sys
from pathlib import Path
import numpy as np
from mathutils import Vector, Matrix, Quaternion

request_path = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
req = json.loads(request_path.read_text(encoding='utf-8-sig'))
assert req['recipe'] == 'industrial_security_v1'
assert tuple(bpy.app.version[:2]) == (5, 2)
assert 1600 <= req['heightMillimetres'] <= 2000 and req['radiusMillimetres'] == 0
assert 0 <= req['seed'] <= 999999
out = request_path.parent / 'output'
out.mkdir(exist_ok=False)
scale = req['heightMillimetres'] / 1800
rng = np.random.default_rng(req['seed'])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system, scene.unit_settings.scale_length = 'METRIC', 1
scene.render.engine = 'CYCLES'
scene.cycles.device, scene.cycles.samples, scene.cycles.use_denoising = 'CPU', 16, True
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.12, .14, .16)
scene.view_settings.view_transform = 'AgX'
parts = []

def ellipsoid(name, centre, radii):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=Vector(centre)*scale)
    obj = bpy.context.object
    obj.name = name
    obj.scale = Vector(radii)*scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(obj)

def limb(name, a, b, radius):
    a, b = Vector(a)*scale, Vector(b)*scale
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=radius*scale, radius2=radius*.9*scale,
                                  depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    parts.append(obj)
    for p in (a/scale, b/scale):
        ellipsoid(name+' joint', p, (radius, radius, radius))

ellipsoid('Continuous suit torso', (0, 0, 1.235), (.24, .135, .29))
ellipsoid('Pelvis', (0, 0, .955), (.20, .135, .17))
ellipsoid('Neck', (0, 0, 1.515), (.073, .075, .10))
ellipsoid('Sealed mannequin head', (0, 0, 1.66), (.115, .105, .14))
for sign in (-1, 1):
    x = sign*.14
    limb('Upper trouser leg', (x, 0, .94), (x, -.005, .54), .095)
    limb('Lower trouser leg', (x, -.005, .54), (x, 0, .16), .075)
    ellipsoid('Boot', (x, -.07, .06), (.095, .18, .06))
    limb('Sleeve upper', (sign*.23, 0, 1.43), (sign*.51, 0, 1.43), .075)
    limb('Sleeve lower', (sign*.51, 0, 1.43), (sign*.73, 0, 1.43), .058)
    ellipsoid('Glove palm', (sign*.775, -.006, 1.43), (.06, .05, .035))

# Voxel union produces one connected deforming surface, rather than detached rigid parts.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
asset = bpy.context.object
asset.name = req['assetName']+'-skin'
remesh = asset.modifiers.new('Continuous original surface', 'REMESH')
remesh.mode, remesh.voxel_size, remesh.use_smooth_shade = 'VOXEL', .018*scale, True
bpy.ops.object.modifier_apply(modifier=remesh.name)
smooth = asset.modifiers.new('Surface relaxation', 'SMOOTH')
smooth.factor, smooth.iterations = .6, 3
bpy.ops.object.modifier_apply(modifier=smooth.name)
scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Canonical exact grounded height after remesh smoothing.
lo = min(v.co.z for v in asset.data.vertices)
hi = max(v.co.z for v in asset.data.vertices)
target = req['heightMillimetres']/1000
for v in asset.data.vertices:
    v.co.z = (v.co.z-lo)*target/(hi-lo)
for p in asset.data.polygons:
    p.use_smooth = True
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.015)
bpy.ops.object.mode_set(mode='OBJECT')

n = 1024
yy, xx = np.mgrid[0:n, 0:n]
weave = ((xx % 4 < 2) ^ (yy % 4 < 2))*.025
noise = rng.random((n, n))*.035
colour = np.empty((n, n, 3), dtype=float)
colour[:] = (.13, .18, .16)
colour += (noise+weave)[:, :, None]
images = {}
for name, pixels, non_colour in [('basecolor', colour, False),
    ('roughness', np.repeat((.72+noise)[:, :, None], 3, axis=2), True),
    ('metallic', np.zeros((n, n, 3)), True)]:
    im = bpy.data.images.new(name, width=n, height=n, alpha=False)
    if non_colour:
        im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(np.concatenate([pixels, np.ones((n, n, 1))], axis=2).astype(np.float32).ravel())
    im.filepath_raw, im.file_format = str(out/(name+'.png')), 'PNG'
    im.save()
    im.pack()
    images[name] = im
suit = bpy.data.materials.new('Original woven training suit')
suit.use_nodes = True
bsdf = suit.node_tree.nodes.get('Principled BSDF')
for name, socket in [('basecolor', 'Base Color'), ('roughness', 'Roughness'), ('metallic', 'Metallic')]:
    tex = suit.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = images[name]
    suit.node_tree.links.new(tex.outputs['Color'], bsdf.inputs[socket])
rubber = bpy.data.materials.new('Boots gloves and helmet seal')
rubber.use_nodes = True
rubber.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (.025, .032, .038, 1)
rubber.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .62
visor = bpy.data.materials.new('Opaque blue visor')
visor.use_nodes = True
vb = visor.node_tree.nodes.get('Principled BSDF')
vb.inputs['Base Color'].default_value = (.035, .075, .095, 1)
vb.inputs['Metallic'].default_value, vb.inputs['Roughness'].default_value = .45, .3
for mat in (suit, rubber, visor):
    asset.data.materials.append(mat)
for poly in asset.data.polygons:
    c = poly.center/scale
    if c.z < .145 or abs(c.x) > .745 or c.z > 1.75:
        poly.material_index = 1
    elif 1.615 < c.z < 1.715 and c.y < -.068:
        poly.material_index = 2


# Layered original uniform; every piece joins the same weighted surface/skin.
# Geometry is authored in metres and receives the existing normalized four-bone field.
metal = bpy.data.materials.new('Worn graphite equipment')
metal.use_nodes = True
mb = metal.node_tree.nodes.get('Principled BSDF')
mb.inputs['Base Color'].default_value = (.055, .065, .07, 1)
mb.inputs['Metallic'].default_value, mb.inputs['Roughness'].default_value = .65, .42
safety = bpy.data.materials.new('Original amber safety trim')
safety.use_nodes = True
sb = safety.node_tree.nodes.get('Principled BSDF')
sb.inputs['Base Color'].default_value, sb.inputs['Roughness'].default_value = (.65, .24, .035, 1), .65
for mat in (metal, safety):
    asset.data.materials.append(mat)
dress = []
def panel(name, location, dimensions, material=3, bevel=.007):
    bpy.ops.mesh.primitive_cube_add(size=1, location=Vector(location)*scale)
    obj = bpy.context.object
    obj.name, obj.dimensions = name, Vector(dimensions)*scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Rounded sewn or machined edges', 'BEVEL')
        mod.width, mod.segments = bevel*scale, 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(asset.data.materials[material])
    dress.append(obj)
    return obj
# Segmented chest/back panels, collar, webbing, stitched seams, zipper and chest radio.
panel('Chest equipment panel', (0,-.132,1.29),(.36,.052,.27),0,.018)
panel('Back equipment panel',(0,.126,1.29),(.34,.045,.27),0,.018)
for x in (-.153,.153):
    panel('Shoulder webbing',(x,-.12,1.43),(.046,.042,.20),1)
    panel('Webbing buckle',(x,-.147,1.39),(.059,.018,.039))
for x in (-.103,0,.103):
    panel('Flap utility pouch',(x,-.176,1.22),(.082,.064,.12),0)
    panel('Pouch flap',(x,-.214,1.26),(.086,.012,.035),1,.003)
    panel('Pouch clasp',(x,-.223,1.245),(.014,.012,.02),3,.002)
panel('Chest radio',(-.106,-.18,1.39),(.064,.045,.09),1)
panel('Radio antenna',(-.12,-.18,1.48),(.007,.007,.12),3,.002)
panel('Original amber chest marker',(.067,-.165,1.38),(.106,.012,.025),4,.002)
panel('Jacket centre zipper',(0,-.148,1.10),(.012,.012,.12),3,.002)
for x in (-.2,.2):
    panel('Jacket edge piping',(x,-.067,1.16),(.011,.013,.22),4,.003)
panel('Belt front',(0,-.125,.975),(.34,.026,.045),1)
panel('Belt back',(0,.125,.975),(.34,.026,.045),1)
panel('Belt buckle',(0,-.149,.975),(.052,.016,.038),3,.003)
# Helmet rim, hard side shells, segmented visor frame; no protected insignia.
panel('Helmet crown',(0,0,1.765),(.21,.18,.055),3,.018)
for s in (-1,1):
    panel('Helmet ear housing',(s*.108,0,1.66),(.034,.15,.11),3,.012)
    panel('Amber helmet band',(s*.109,-.02,1.72),(.036,.15,.014),4,.002)
    panel('Visor side rail',(s*.084,-.096,1.66),(.016,.034,.10),3,.005)
panel('Visor top rim',(0,-.103,1.709),(.18,.026,.014),3,.003)
panel('Visor lower rim',(0,-.104,1.614),(.18,.026,.014),3,.003)
panel('Respirator front',(0,-.114,1.579),(.10,.055,.057),1,.012)
for x in (-.03,0,.03):
    panel('Respirator grille',(x,-.145,1.58),(.006,.008,.028),3,.001)
for s in (-1,1):
    panel('Sleeve cuff',(s*.701,0,1.43),(.055,.12,.10),1,.009)
    panel('Sleeve seam',(s*.48,-.066,1.43),(.30,.008,.012),4,.002)
    panel('Knee reinforced pad',(s*.14,-.075,.54),(.128,.044,.14),1,.018)
    for z in (.505,.54,.575):
        panel('Knee pad rib',(s*.14,-.104,z),(.10,.015,.011),3,.003)
    panel('Cargo pocket',(s*.21,-.035,.78),(.074,.115,.145),0,.009)
    panel('Cargo pocket flap',(s*.217,-.035,.845),(.083,.125,.028),1,.004)
    panel('Boot toe cap',(s*.14,-.14,.06),(.16,.15,.064),1,.016)
    panel('Boot sole',(s*.14,-.063,.022),(.17,.30,.032),1,.005)
    for z in (.075,.101,.127):
        panel('Boot lace',(s*.14,-.075,z),(.10,.012,.009),3,.002)
    # Individually visible glove fingers, with joint grooves. Finger articulation is not yet authored.
    for f in range(4):
        panel('Glove finger',(s*(.818+.012*(1-abs(f-1.5))),-.040+f*.026,1.43),
              (.080,.020,.028),1,.008)
    panel('Glove thumb',(s*.77,-.068,1.42),(.040,.052,.028),1,.008)
# Join without voxel union to preserve layered detail and UV seams.
bpy.ops.object.select_all(action='DESELECT')
asset.select_set(True)
for obj in dress:
    obj.select_set(True)
bpy.context.view_layer.objects.active = asset
bpy.ops.object.join()
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.012)
bpy.ops.object.mode_set(mode='OBJECT')
# Tangent-space cloth normal map, authored from a periodic weave field, not external texture data.
normal = np.empty((n,n,3), dtype=float)
normal[:,:,0] = .5 + .045*np.sin(xx*math.pi/2)
normal[:,:,1] = .5 + .045*np.sin(yy*math.pi/2)
normal[:,:,2] = np.sqrt(np.maximum(0,1-(normal[:,:,0]*2-1)**2-(normal[:,:,1]*2-1)**2))*.5+.5
im = bpy.data.images.new('Original cloth normal',width=n,height=n,alpha=False)
im.colorspace_settings.name = 'Non-Color'
im.pixels.foreach_set(np.concatenate([normal,np.ones((n,n,1))],axis=2).astype(np.float32).ravel())
im.filepath_raw,im.file_format = str(out/'normal.png'),'PNG'
im.save()
im.pack()
tex = suit.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = im
nm = suit.node_tree.nodes.new('ShaderNodeNormalMap')
nm.inputs['Strength'].default_value = .35
suit.node_tree.links.new(tex.outputs['Color'],nm.inputs['Color'])
suit.node_tree.links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])

arm_data = bpy.data.armatures.new('ForgeCore generic humanoid skeleton v1')
arm = bpy.data.objects.new(req['assetName']+'-rig', arm_data)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
asset.select_set(False)
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
definitions = [('Root', (0, 0, 0), (0, 0, .1), None),
    ('Hips', (0, 0, .94), (0, 0, 1.07), 'Root'),
    ('Spine', (0, 0, 1.07), (0, 0, 1.25), 'Hips'),
    ('Chest', (0, 0, 1.25), (0, 0, 1.46), 'Spine'),
    ('Neck', (0, 0, 1.46), (0, 0, 1.55), 'Chest'),
    ('Head', (0, 0, 1.55), (0, 0, 1.78), 'Neck')]
for side, s in [('L', 1), ('R', -1)]:
    def bone(name, a, b, parent):
        definitions.append((name+'.'+side, a, b, parent if parent in ('Chest', 'Hips') else parent+'.'+side))
    bone('Clavicle', (0, 0, 1.43), (s*.23, 0, 1.43), 'Chest')
    bone('UpperArm', (s*.23, 0, 1.43), (s*.51, 0, 1.43), 'Clavicle')
    bone('LowerArm', (s*.51, 0, 1.43), (s*.73, 0, 1.43), 'UpperArm')
    bone('Hand', (s*.73, 0, 1.43), (s*.86, 0, 1.43), 'LowerArm')
    bone('UpperLeg', (s*.14, 0, .94), (s*.14, -.005, .54), 'Hips')
    bone('LowerLeg', (s*.14, -.005, .54), (s*.14, 0, .16), 'UpperLeg')
    bone('Foot', (s*.14, 0, .16), (s*.14, -.14, .065), 'LowerLeg')
    bone('Toe', (s*.14, -.14, .065), (s*.14, -.235, .065), 'Foot')
for name, a, b, parent in definitions:
    bnew = arm_data.edit_bones.new(name)
    bnew.head, bnew.tail = Vector(a)*scale, Vector(b)*scale
    if parent:
        bnew.parent = arm_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
arm.show_in_front = True
asset.parent = arm
modifier = asset.modifiers.new('Real four-influence skeletal skin', 'ARMATURE')
modifier.object = arm
groups = {name: asset.vertex_groups.new(name=name) for name, _, _, _ in definitions if name != 'Root'}
def smooth(a, b, value):
    t = min(1, max(0, (value-a)/(b-a)))
    return t*t*(3-2*t)
def along(value, centres):
    if value <= centres[0][0]:
        return {centres[0][1]: 1}
    for (a, first), (b, second) in zip(centres, centres[1:]):
        if value <= b:
            if first == second:
                return {first: 1}
            t = smooth(a, b, value)
            return {first: 1-t, second: t}
    return {centres[-1][1]: 1}
for vertex in asset.data.vertices:
    x, y, z = vertex.co/scale
    side = 'L' if x >= 0 else 'R'
    if z < .97:
        if z > .65:
            # Continuous pelvis field. Avoid selecting/dropping a fourth-nearest
            # bone abruptly on short crotch edges or binding a thigh to the other knee.
            leg = 1-smooth(.74, .97, z)
            left = smooth(-.075, .075, x)
            weights = {'Hips': 1-leg, 'UpperLeg.L': leg*left, 'UpperLeg.R': leg*(1-left)}
        elif z > .18:
            weights = along(z, [(.18, 'LowerLeg.'+side), (.35, 'LowerLeg.'+side), (.65, 'UpperLeg.'+side)])
        else:
            lower = smooth(.10, .18, z)
            toe = 1-smooth(-.20, -.105, y)
            weights = {'LowerLeg.'+side: lower, 'Foot.'+side: (1-lower)*(1-toe), 'Toe.'+side: (1-lower)*toe}
    else:
        weights = along(z, [(.99, 'Hips'), (1.16, 'Spine'), (1.35, 'Chest'), (1.505, 'Neck'), (1.665, 'Head')])
        arm_blend = smooth(.17, .31, abs(x))*(1-smooth(.14, .25, abs(z-1.43)))
        if arm_blend > 0:
            limb_weights = along(abs(x), [(.15, 'Clavicle.'+side), (.37, 'UpperArm.'+side),
                                         (.62, 'LowerArm.'+side), (.80, 'Hand.'+side)])
            weights = {name: weight*(1-arm_blend) for name, weight in weights.items()}
            for name, weight in limb_weights.items():
                weights[name] = weights.get(name, 0)+weight*arm_blend
    weights = {name: value for name, value in weights.items() if value > .0000001}
    assert len(weights) <= 4
    total = sum(weights.values())
    for name, weight in weights.items():
        groups[name].add([vertex.index], weight/total, 'REPLACE')
weight_error = max(abs(sum(g.weight for g in v.groups)-1) for v in asset.data.vertices)
assert weight_error < .00001
asset.data.calc_loop_triangles()
rest_vertices = [v.co.copy() for v in asset.data.vertices]
for edge in asset.data.edges:
    a, b = [rest_vertices[i]/scale for i in edge.vertices]
    assert not (a.z < .65 and b.z < .65 and a.x*b.x < 0), 'Unwanted remesh bridge between separated legs'

def source_pose_turn(name, axis, degrees):
    pb = arm.pose.bones[name]
    pivot = pb.head.copy()
    pb.matrix = Matrix.Translation(pivot) @ Quaternion(Vector(axis), math.radians(degrees)).to_matrix().to_4x4() @ Matrix.Translation(-pivot) @ pb.matrix
    bpy.context.view_layer.update()
source_poses = []
for label in ('arms', 'legs', 'combined'):
    if label == 'combined':
        source_pose_turn('Hips', (0, 0, 1), 15)
        source_pose_turn('Chest', (0, 1, 0), 10)
    if label in ('arms', 'combined'):
        source_pose_turn('UpperArm.L', (0, 1, 0), 30)
        source_pose_turn('LowerArm.L', (0, 0, 1), -65)
        source_pose_turn('UpperArm.R', (0, 1, 0), -30)
        source_pose_turn('LowerArm.R', (0, 0, 1), 65)
    if label in ('legs', 'combined'):
        source_pose_turn('UpperLeg.L', (1, 0, 0), -35)
        source_pose_turn('LowerLeg.L', (1, 0, 0), 70)
        source_pose_turn('UpperLeg.R', (1, 0, 0), 25)
        source_pose_turn('LowerLeg.R', (1, 0, 0), -45)
    evaluated = asset.evaluated_get(bpy.context.evaluated_depsgraph_get())
    posed_mesh = evaluated.to_mesh()
    posed = [v.co.copy() for v in posed_mesh.vertices]
    worst, worst_point = 1, None
    for edge in asset.data.edges:
        a, b = edge.vertices
        length = (rest_vertices[a]-rest_vertices[b]).length
        if length > .00001:
            stretch = (posed[a]-posed[b]).length/length
            if stretch > worst:
                worst, worst_point = stretch, list(rest_vertices[a])
    evaluated.to_mesh_clear()
    print('SOURCE_POSE', label, 'maximum_edge_stretch', worst, 'near', worst_point, flush=True)
    assert worst <= 3.5, 'Original source skin fails diagnostic stretch gate'
    source_poses.append(dict(pose=label, maximumEdgeStretch=worst))
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
bounds = [asset.matrix_world @ Vector(c) for c in asset.bound_box]
metrics = dict(recipe=req['recipe'], blenderVersion=bpy.app.version_string, vertices=len(asset.data.vertices),
    triangles=len(asset.data.loop_triangles), materials=len(asset.data.materials), objects=2,
    boundsMin=[min(v[k] for v in bounds) for k in range(3)], boundsMax=[max(v[k] for v in bounds) for k in range(3)],
    pivot='bottom_center', units='metres', textureResolution=1024, animationCount=0)
assert 100 <= metrics['vertices'] <= 30000 and metrics['triangles'] <= 60000
rig = dict(schemaVersion=1, rigConvention='generic_named_bones_v1', restPose='T', sourceForward='-Y', sourceUp='+Z',
    unityForward='+Z', rootMotion='in_place_no_motion_clips', boneNames=[b.name for b in arm_data.bones],
    maximumInfluences=4, unweightedVertices=0, maximumWeightSumError=weight_error, sourceDiagnosticPoses=source_poses)
for name, payload in [('metrics.json', metrics), ('rig.json', rig)]:
    with (out/name).open('x', encoding='utf-8') as f:
        json.dump(payload, f, separators=(',', ':'))
bpy.ops.object.select_all(action='DESELECT')
asset.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.gltf(filepath=str(out/'asset.glb'), export_format='GLB', use_selection=True, export_apply=False,
    export_skins=True, export_influence_nb=4, export_all_influences=False, export_def_bones=False,
    export_rest_position_armature=True, export_materials='EXPORT', export_animations=False, export_cameras=False, export_lights=False)

# Source contains a studio stage; only the mesh/skeleton above were exported.
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.002))
floor = bpy.context.object
floor.name = 'Studio floor NOT exported'
fm = bpy.data.materials.new('Studio charcoal')
fm.diffuse_color = (.035, .045, .05, 1)
floor.data.materials.append(fm)
height = target
for name, location, energy, colour in [('Key', (2, -3, 4), 500, (1, .9, .8)),
    ('Rim', (-2, 1, 3), 500, (.7, .85, 1)), ('Fill', (-2, -2, 2), 160, (1, 1, 1))]:
    light = bpy.data.lights.new(name, 'AREA')
    light.energy, light.shape, light.size, light.color = energy, 'DISK', 3, colour
    ob = bpy.data.objects.new(name, light)
    scene.collection.objects.link(ob)
    ob.location = location
    ob.rotation_euler = (Vector((0, 0, height*.5))-ob.location).to_track_quat('-Z', 'Y').to_euler()
camera = bpy.data.objects.new('Review camera', bpy.data.cameras.new('Review camera'))
scene.collection.objects.link(camera)
camera.location = (height*.9, -height*2.2, height*.95)
camera.rotation_euler = (Vector((0, 0, height*.5))-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type, camera.data.ortho_scale = 'ORTHO', height*1.3
scene.camera = camera
scene.render.filepath = str(out/'preview.png')
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
asset.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.wm.save_as_mainfile(filepath=str(out/'model.blend'), compress=False, check_existing=False)
bpy.ops.render.render(write_still=True)
print('FORGECORE_BLENDER_RECIPE_COMPLETE')
