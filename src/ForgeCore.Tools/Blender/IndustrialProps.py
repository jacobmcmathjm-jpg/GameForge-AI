"""ForgeCore S08 fixed industrial props; original geometry/textures, no external art, scripts or network."""
import bpy
import json
import math
import sys
from pathlib import Path
import numpy as np
from mathutils import Vector

request_path = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
with request_path.open(encoding='utf-8-sig') as stream:
    req = json.load(stream)
assert tuple(bpy.app.version[:2]) == (5, 2)
recipes = {
    'industrial_barrel_v2': (.68, .68, 1.1, 1000, 1500),
    'wooden_crate_v1': (1., .8, .8, 600, 1200),
    'concrete_barrier_v1': (2.4, .6, 1., 800, 1200),
    'industrial_fence_v1': (3., .16, 2.2, 1800, 2600),
    'shipping_container_v1': (2.44, 6.06, 2.59, 2200, 3000)}
recipe = req['recipe']
assert recipe in recipes and 0 <= req['seed'] <= 999999
w, d, nominal, minimum, maximum = recipes[recipe]
assert minimum <= req['heightMillimetres'] <= maximum
h = req['heightMillimetres'] / 1000.
w, d = w*h/nominal, d*h/nominal
if recipe == 'industrial_barrel_v2':
    assert 250 <= req['radiusMillimetres'] <= 500
    w = d = req['radiusMillimetres']/500.
else:
    assert req['radiusMillimetres'] == 0
out = request_path.parent/'output'
out.mkdir(exist_ok=False)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
rng = np.random.default_rng(req['seed'])
n = 1024
yy, xx = np.mgrid[0:n, 0:n].astype(float)/n
fine = rng.random((n, n))
grain = sum(np.sin(xx*math.tau*(3+k*7)+rng.uniform(0, math.tau)) *
            np.sin(yy*math.tau*(2+k*3)+rng.uniform(0, math.tau))/(k+1) for k in range(6))
wear = np.clip((grain-.14)*1.6, 0, 1)
wood = recipe == 'wooden_crate_v1'
concrete = recipe == 'concrete_barrier_v1'
if wood:
    lines = np.sin((xx*90+np.sin(yy*13)*.8)*math.tau)
    shade = .85+lines*.10+fine*.12
    colour = np.array([.28, .16, .065])[None, None, :]*shade[:, :, None]
    relief = lines*.04+grain*.012
elif concrete:
    shade = .82+fine*.22+grain*.035
    colour = np.array([.34, .33, .29])[None, None, :]*shade[:, :, None]
    stripes = ((xx+yy*.4)*12 % 1) > .50
    band = ((yy > .62) & (yy < .78))[:, :, None]
    paint = np.where(stripes[:, :, None], np.array([.62, .40, .065]), np.array([.045, .05, .045]))
    colour = np.where(band, colour*.25+paint*.75, colour)
    relief = fine*.08+grain*.025
else:
    paint = np.array([.045, .13, .115]) if recipe != 'shipping_container_v1' else np.array([.085, .155, .20])
    rust = np.stack([.24+fine*.12, .07+fine*.04, .025+fine*.02], axis=-1)
    colour = paint[None, None, :]*(.85+fine[:, :, None]*.22)*(1-wear[:, :, None])+rust*wear[:, :, None]
    relief = wear*.06+fine*.015
roughness = np.clip(.7+fine*.10 if wood or concrete else .38+wear*.5+fine*.05, 0, 1)
metallic = np.zeros((n, n)) if wood or concrete else np.clip(.8-wear*.75, 0, 1)
gy, gx = np.gradient(relief)
normals = np.stack([-gx*70, -gy*70, np.ones_like(gx)], axis=-1)
normals /= np.linalg.norm(normals, axis=-1)[:, :, None]

def image(name, pixels, noncolour=False):
    if pixels.ndim == 2:
        pixels = np.repeat(pixels[:, :, None], 3, axis=2)
    rgba = np.concatenate([np.clip(pixels, 0, 1), np.ones((n, n, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new(name, width=n, height=n, alpha=False)
    if noncolour:
        im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = str(out/(name+'.png'))
    im.file_format = 'PNG'
    im.save()
    im.pack()
    return im

base = image('basecolor', colour)
rough = image('roughness', roughness, True)
metal = image('metallic', metallic, True)
normal = image('normal', normals*.5+.5, True)
material = bpy.data.materials.new('Original '+recipe+' image PBR finish')
material.use_nodes = True
nodes, links = material.node_tree.nodes, material.node_tree.links
bsdf = nodes.get('Principled BSDF')
for im, socket in [(base, 'Base Color'), (rough, 'Roughness'), (metal, 'Metallic')]:
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = im
    links.new(tex.outputs['Color'], bsdf.inputs[socket])
tex = nodes.new('ShaderNodeTexImage')
tex.image = normal
nm = nodes.new('ShaderNodeNormalMap')
links.new(tex.outputs['Color'], nm.inputs['Color'])
links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
trim = bpy.data.materials.new('Original dark fittings')
trim.use_nodes = True
trim_bsdf = trim.node_tree.nodes.get('Principled BSDF')
trim_bsdf.inputs['Base Color'].default_value = (.055, .065, .055, 1)
trim_bsdf.inputs['Metallic'].default_value = .65
trim_bsdf.inputs['Roughness'].default_value = .55
parts = []

def box(name, centre, size, dark=False, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(trim if dark else material)
    if bevel:
        modifier = ob.modifiers.new('Small manufactured edge radius', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    parts.append(ob)
    return ob

def cylinder(name, centre, radius, depth, sides, dark=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=centre)
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.append(trim if dark else material)
    for face in ob.data.polygons:
        face.use_smooth = abs(face.normal.z) < .9
    parts.append(ob)
    return ob

def beam(name, a, b, thick, dark=False):
    delta = Vector(b)-Vector(a)
    ob = box(name, (Vector(a)+Vector(b))*.5, (thick, thick, delta.length), dark)
    ob.rotation_euler = delta.to_track_quat('Z', 'Y').to_euler()
    return ob

def make_lod(level):
    global parts
    parts = []
    if recipe == 'industrial_barrel_v2':
        sides = [64, 32, 12][level]
        cylinder('Pressed steel barrel', (0, 0, h/2), w/2*.975, h, sides)
        for z in [.025, .27, .73, .975]:
            cylinder('Rolled rib', (0, 0, z*h), w/2, .035*h, sides, True)
        if level < 2:
            cylinder('Threaded bung', (w*.25, 0, h*.989), w*.065, h*.022, 16 if level == 0 else 8, True)
        if level == 0:
            cylinder('Vent', (-w*.25, 0, h*.986), w*.028, h*.028, 12, True)
            for z in [.08, .92]:
                cylinder('Fine pressed seam', (0, 0, z*h), w*.492, .008*h, sides, True)
    elif recipe == 'wooden_crate_v1':
        count = [7, 4, 1][level]
        for side in [-1, 1]:
            for i in range(count):
                z = (i+.5)*h/count
                box('Long wooden board', (0, side*(d/2-.025), z), (w, .05, h/count*.985), bevel=.003 if level == 0 else 0)
                box('Short wooden board', (side*(w/2-.025), 0, z), (.05, d-.1, h/count*.985))
        box('Closed crate lid', (0, 0, h-.025), (w, d, .05))
        box('Crate base', (0, 0, .025), (w, d, .05))
        if level < 2:
            for side in [-1, 1]:
                for x in [-w*.4, w*.4]:
                    box('Corner brace', (x, side*(d/2-.04), h/2), (.07, .08, h), True)
                beam('Diagonal brace', (-w*.40, side*(d/2-.04), h*.12), (w*.40, side*(d/2-.04), h*.88), .045, True)
        if level == 0:
            for x in [-w*.4, w*.4]:
                for z in [h*.12, h*.88]:
                    box('Recessed steel fastener', (x, -d/2+.002, z), (.022, .008, .022), True)
    elif recipe == 'concrete_barrier_v1':
        # Convex jersey-like cross section; collision deliberately remains its conservative solid box.
        profile = [(-d/2, 0), (d/2, 0), (d/2, h*.22), (d*.18, h*.65), (d*.18, h), (-d*.18, h), (-d*.18, h*.65), (-d/2, h*.22)]
        vertices = [(x, y, z) for x in [-w/2, w/2] for y, z in profile]
        count = len(profile)
        faces = [tuple(reversed(range(count))), tuple(range(count, count*2))]
        faces += [(i, (i+1)%count, (i+1)%count+count, i+count) for i in range(count)]
        mesh = bpy.data.meshes.new('Concrete convex profile')
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        ob = bpy.data.objects.new('Concrete barrier', mesh)
        scene.collection.objects.link(ob)
        ob.data.materials.append(material)
        parts.append(ob)
        if level < 2:
            for x in [-w*.35, w*.35]:
                box('Recessed lifting insert', (x, 0, h-.008), (.09, .09, .016), True)
        if level == 0:
            for x in [-w*.32, w*.32]:
                box('Mould seam', (x, 0, .018), (.012, d, .036), True)
    elif recipe == 'industrial_fence_v1':
        for x in [-w/2+.035, w/2-.035]:
            box('Square steel post', (x, 0, h/2), (.07, d, h), True)
        for z in [.04, h-.04]:
            box('Horizontal frame', (0, 0, z), (w, .065, .06), True)
        for i in range([21, 11, 5][level]):
            count = [21, 11, 5][level]
            x = -w/2+.1+(w-.2)*i/max(1, count-1)
            box('Welded vertical wire', (x, 0, h/2), (.013, .013, h-.12))
        for i in range([13, 7, 3][level]):
            count = [13, 7, 3][level]
            z = .12+(h-.24)*i/max(1, count-1)
            box('Welded horizontal wire', (0, .012, z), (w-.14, .013, .013))
        if level == 0:
            for x in [-w/2+.035, w/2-.035]:
                for z in [.18, h-.18]:
                    box('Panel clamp', (x, -.045, z), (.06, .035, .08), True)
    else:
        box('Closed container body', (0, 0, h/2), (w*.96, d*.99, h*.96), bevel=.012 if level == 0 else 0)
        for x in [-w/2+.04, w/2-.04]:
            for y in [-d/2+.04, d/2-.04]:
                box('Corner casting column', (x, y, h/2), (.08, .08, h), True)
        ribs = [25, 13, 5][level]
        for side in [-1, 1]:
            for i in range(ribs):
                y = -d*.46+d*.92*i/max(1, ribs-1)
                box('Side corrugation', (side*w*.49, y, h*.50), (w*.02, d/ribs*.22, h*.87))
            box('End door panel', (0, side*(d/2-.015), h/2), (w*.95, .03, h*.94))
            if level < 2:
                for x in [-w*.30, w*.30]:
                    box('Locking rod', (x, side*(d/2-.008), h/2), (.025, .016, h*.84), True)
        if level == 0:
            for x in [-w*.30, w*.30]:
                for side in [-1, 1]:
                    box('Latch handle', (x, side*(d/2-.006), h*.38), (.18, .012, .035), True)
            for i in range(10):
                box('Roof rib', (0, -d*.43+i*d*.086, h*.985), (w*.92, .06, h*.03))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    asset = bpy.context.object
    asset.name = req['assetName'] if level == 0 else req['assetName']+'_LOD'+str(level)
    scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # Identity at grounded origin, with all geometry already in local metre coordinates.
    assert asset.location.length < .00001
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    asset.data.calc_loop_triangles()
    assert all(math.isfinite(v) for uv in asset.data.uv_layers.active.data for v in uv.uv)
    return asset

assets, triangle_counts = [], []
for level in range(3):
    asset = make_lod(level)
    assets.append(asset)
    triangle_counts.append(len(asset.data.loop_triangles))
    filename = 'asset.glb' if level == 0 else 'lod'+str(level)+'.glb'
    bpy.ops.export_scene.gltf(filepath=str(out/filename), export_format='GLB', use_selection=True,
        export_apply=True, export_materials='EXPORT', export_animations=False, export_cameras=False, export_lights=False)
    asset.hide_render = level != 0
assert 12 <= triangle_counts[2] < triangle_counts[1] < triangle_counts[0] <= 16000
main_asset = assets[0]
bounds = [main_asset.matrix_world@Vector(c) for c in main_asset.bound_box]
metrics = dict(recipe=recipe, blenderVersion=bpy.app.version_string, vertices=len(main_asset.data.vertices),
    triangles=triangle_counts[0], materials=len(main_asset.data.materials), objects=1,
    boundsMin=[min(v[k] for v in bounds) for k in range(3)], boundsMax=[max(v[k] for v in bounds) for k in range(3)],
    pivot='bottom_center', units='metres', textureResolution=n, animationCount=0)
prop = dict(schemaVersion=1, recipe=recipe, collisionConvention='solid_box_no_walkable_interior',
    collisionCentre=[0, 0, h/2], collisionSize=[w, d, h], lodTriangles=triangle_counts,
    uvFinite=True, rootIdentity=True, coordinateSystem='Blender_Z_up_metres')
for name, value in [('metrics.json', metrics), ('prop.json', prop)]:
    with (out/name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, separators=(',', ':'))

scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.12, .14, .16)
scene.view_settings.view_transform = 'AgX'
bpy.ops.mesh.primitive_plane_add(size=max(w, d, h)*12, location=(0, 0, -.003))
floor = bpy.context.object
floor.name = 'Review stage NOT exported'
floor_mat = bpy.data.materials.new('Review charcoal')
floor_mat.diffuse_color = (.045, .055, .065, 1)
floor.data.materials.append(floor_mat)
extent = max(w, d, h)
target = Vector((0, 0, h*.48))
for name, location, power, colour in [
    ('Warm key', (extent, -extent, extent*1.8), 550, (1, .88, .73)),
    ('Cool rim', (-extent, extent, extent*1.5), 700, (.70, .84, 1)),
    ('Fill', (-extent, -extent, extent), 200, (1, 1, 1))]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.size, data.color = power*max(1, extent), extent, colour
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    ob.location = location
    ob.rotation_euler = (target-ob.location).to_track_quat('-Z', 'Y').to_euler()
camera_data = bpy.data.cameras.new('Review camera')
camera = bpy.data.objects.new('Review camera', camera_data)
scene.collection.objects.link(camera)
camera.location = (extent*1.4, -extent*1.8, extent*1.25)
camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.type = 'ORTHO'
camera_data.ortho_scale = extent*1.5
scene.camera = camera
scene.render.filepath = str(out/'preview.png')
bpy.ops.object.select_all(action='DESELECT')
main_asset.select_set(True)
bpy.context.view_layer.objects.active = main_asset
bpy.ops.wm.save_as_mainfile(filepath=str(out/'model.blend'), compress=False, check_existing=False)
bpy.ops.render.render(write_still=True)
print('FORGECORE_BLENDER_RECIPE_COMPLETE')
