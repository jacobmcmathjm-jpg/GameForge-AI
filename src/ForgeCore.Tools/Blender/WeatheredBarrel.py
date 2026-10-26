"""ForgeCore weathered_barrel_v1. Original bounded recipe; no external inputs or network."""
import bpy
import json
import math
import sys
from pathlib import Path
import numpy as np
from mathutils import Vector

request_path = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
with request_path.open(encoding='utf-8-sig') as f:
    req = json.load(f)
assert req['recipe'] == 'weathered_barrel_v1'
assert tuple(bpy.app.version[:2]) == (5, 2), 'This recipe is verified only for Blender 5.2'
assert 1000 <= req['heightMillimetres'] <= 1500 and 250 <= req['radiusMillimetres'] <= 500
assert 0 <= req['seed'] <= 999999
out = request_path.parent / 'output'
out.mkdir(exist_ok=False)
height, radius = req['heightMillimetres'] / 1000, req['radiusMillimetres'] / 1000
rng = np.random.default_rng(req['seed'])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 768
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.world.color = (0.16, 0.16, 0.16)
scene.view_settings.view_transform = 'AgX'

# A repeatable, image-based PBR finish. These images transfer through glTF;
# Blender-only noise nodes would not. No stock art, fonts, downloads or API keys.
n = 512
y, x = np.mgrid[0:n, 0:n].astype(float) / n
phase = rng.uniform(0, math.tau, 9)
noise = sum(np.sin(x * math.tau * (k * 3 + 2) + phase[k]) *
            np.sin(y * math.tau * (k * 2 + 3) + phase[8-k]) / (k+1) for k in range(9))
fine = rng.random((n, n))
rust = np.clip((noise - 0.13) * 2.5, 0, 1)
scratches = (np.sin(x * math.tau * 173 + np.sin(y * 19) * 0.6) > 0.996) * (fine > 0.35)
rust = np.maximum(rust, scratches * 0.85)
paint = np.array([0.065, 0.19, 0.135])
oxide = np.stack([0.20 + fine * 0.12, 0.055 + fine * 0.04, 0.018 + fine * 0.018], axis=-1)
colour = paint[None, None, :] * (0.82 + fine[:, :, None] * 0.26) * (1-rust[:, :, None]) + oxide * rust[:, :, None]
# Faded cream industrial stripe; broken by the same wear rather than a pristine decal.
stripe = ((y > 0.62) & (y < 0.69))[:, :, None] * (1-rust[:, :, None]) * 0.55
colour = colour * (1-stripe) + np.array([0.56, 0.50, 0.30]) * stripe

def image(name, rgb, non_colour=False):
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[:, :, None], 3, axis=2)
    pixels = np.concatenate([rgb, np.ones((n, n, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new(name, width=n, height=n, alpha=False)
    if non_colour:
        im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(pixels.ravel())
    im.filepath_raw = str(out / (name + '.png'))
    im.file_format = 'PNG'
    im.save()
    im.pack()
    return im

base = image('basecolor', colour)
rough = image('roughness', np.clip(0.42 + rust * 0.42 + fine * 0.08, 0, 1), True)
metal = image('metallic', np.clip(0.82 - rust * 0.74, 0, 1), True)
painted = bpy.data.materials.new('Weathered green paint and exposed oxidised steel')
painted.use_nodes = True
nodes = painted.node_tree.nodes
bsdf = nodes.get('Principled BSDF')
for im, socket in [(base, 'Base Color'), (rough, 'Roughness'), (metal, 'Metallic')]:
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = im
    painted.node_tree.links.new(tex.outputs['Color'], bsdf.inputs[socket])
steel = bpy.data.materials.new('Dark worn rim steel')
steel.use_nodes = True
s = steel.node_tree.nodes.get('Principled BSDF')
s.inputs['Base Color'].default_value = (0.09, 0.07, 0.045, 1)
s.inputs['Metallic'].default_value = 0.82
s.inputs['Roughness'].default_value = 0.53

parts = []
def cylinder(name, rad, depth, z, material, vertices=64, xy=(0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=rad, depth=depth, end_fill_type='NGON',
        location=(xy[0], xy[1], z))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    bevel = obj.modifiers.new('Manufactured rounded edges', 'BEVEL')
    bevel.width = min(0.006, depth / 4)
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for poly in obj.data.polygons:
        # Keep lids flat and side walls smooth.
        poly.use_smooth = abs(poly.normal.z) < 0.9
    parts.append(obj)
    return obj

# Radial profile with rolled reinforcing ribs and a seam; UV is cylindrical.
profile = [(0.012, .97), (.035, 1), (.06, 1), (.075, .98), (.22, .98),
           (.235, 1.015), (.255, 1.015), (.27, .98), (.70, .98),
           (.715, 1.015), (.735, 1.015), (.75, .98), (.94, .98), (.965, 1), (.985, 1)]
segments = 96
verts = [(radius*r*math.cos(i*math.tau/segments), radius*r*math.sin(i*math.tau/segments), z*height)
         for z, r in profile for i in range(segments)]
faces = []
for j in range(len(profile)-1):
    for i in range(segments):
        faces.append((j*segments+i, j*segments+(i+1)%segments, (j+1)*segments+(i+1)%segments, (j+1)*segments+i))
mesh = bpy.data.meshes.new('Barrel ribbed shell mesh')
mesh.from_pydata(verts, [], faces)
mesh.update()
body = bpy.data.objects.new('Barrel ribbed steel shell', mesh)
scene.collection.objects.link(body)
body.data.materials.append(painted)
uv = mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    j, i = divmod(poly.index, segments)
    for li, coord in zip(poly.loop_indices, [(i/segments, profile[j][0]), ((i+1)/segments, profile[j][0]),
                                           ((i+1)/segments, profile[j+1][0]), (i/segments, profile[j+1][0])]):
        uv.data[li].uv = coord
    poly.use_smooth = True
parts.append(body)
def rim(name, bottom, top):
    # A rim is annular, not a capped cylinder: a solid cap would hide lid details.
    profile = [(radius*.96, bottom), (radius*1.025, bottom), (radius*1.025, top), (radius*.96, top)]
    rv = [(r*math.cos(i*math.tau/96), r*math.sin(i*math.tau/96), z) for r, z in profile for i in range(96)]
    rf = [(j*96+i, j*96+(i+1)%96, ((j+1)%4)*96+(i+1)%96, ((j+1)%4)*96+i)
          for j in range(4) for i in range(96)]
    rm = bpy.data.meshes.new(name)
    rm.from_pydata(rv, [], rf)
    rm.update()
    ro = bpy.data.objects.new(name, rm)
    scene.collection.objects.link(ro)
    ro.data.materials.append(steel)
    for p in rm.polygons:
        p.use_smooth = abs(p.normal.z) < .9
    parts.append(ro)
rim('Bottom rolled rim', 0, .025*height)
rim('Top rolled rim', .975*height, height)
cylinder('Recessed pressed lid', radius*.965, .012*height, .971*height, painted)
cylinder('Sealed bottom', radius*.965, .012*height, .022*height, painted)
cylinder('Large threaded bung surround', radius*.115, .012*height, .982*height, steel, 32, (radius*.53, 0))
cylinder('Large bung cap', radius*.08, .013*height, .988*height, steel, 8, (radius*.53, 0))
cylinder('Small vent cap', radius*.045, .012*height, .984*height, steel, 16, (-radius*.53, 0))

# Grounded bottom pivot and identity transform on the joined export object.
bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
asset = bpy.context.object
asset.name = req['assetName']
scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
mesh = asset.data
mesh.calc_loop_triangles()
bounds = [asset.matrix_world @ Vector(c) for c in asset.bound_box]
metrics = dict(recipe=req['recipe'], blenderVersion=bpy.app.version_string,
    vertices=len(mesh.vertices), triangles=len(mesh.loop_triangles), materials=len(mesh.materials),
    objects=1, boundsMin=[min(v[k] for v in bounds) for k in range(3)],
    boundsMax=[max(v[k] for v in bounds) for k in range(3)],
    pivot='bottom_center', units='metres', textureResolution=n, animationCount=0)
assert metrics['triangles'] <= 20000 and metrics['materials'] <= 4

# Export only the asset, never the preview stage/camera/lights; embedded textures.
bpy.ops.export_scene.gltf(filepath=str(out/'asset.glb'), export_format='GLB', use_selection=True,
    export_apply=True, export_materials='EXPORT', export_animations=False, export_cameras=False, export_lights=False)
with (out/'metrics.json').open('x', encoding='utf-8') as f:
    json.dump(metrics, f, separators=(',', ':'))

# Original studio review stage, retained editable in the source but not exported.
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.002))
floor = bpy.context.object
floor.name = 'Preview stage NOT exported'
mat = bpy.data.materials.new('Preview charcoal')
mat.diffuse_color = (.035, .045, .05, 1)
floor.data.materials.append(mat)
def area(name, loc, power, size, colour):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.shape, data.size, data.color = power, 'DISK', size, colour
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (Vector((0, 0, height*.5))-ob.location).to_track_quat('-Z', 'Y').to_euler()
area('Warm soft key', (2, -3, 4), 450, 3, (1, .90, .78))
area('Cool rim', (-2, 1.5, 2.8), 550, 2, (.7, .83, 1))
area('Front fill', (-2, -2, 1.5), 100, 3, (1, 1, 1))
cam_data = bpy.data.cameras.new('Review camera')
cam = bpy.data.objects.new('Review camera', cam_data)
scene.collection.objects.link(cam)
cam.location = (height*1.65, -height*2.4, height*1.8)
cam.rotation_euler = (Vector((0, 0, height*.48))-cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.type = 'ORTHO'
cam_data.ortho_scale = height*1.65
scene.camera = cam
bpy.ops.object.select_all(action='DESELECT')
asset.select_set(True)
bpy.context.view_layer.objects.active = asset
scene.render.filepath = str(out/'preview.png')
# Relative packed textures; save before render so a render failure never publishes a bundle.
bpy.ops.wm.save_as_mainfile(filepath=str(out/'model.blend'), compress=False, check_existing=False)
bpy.ops.render.render(write_still=True)
print('FORGECORE_BLENDER_RECIPE_COMPLETE')
