"""ForgeCore refined industrial candidates. Original local geometry/PBR, never a human art approval.
Legacy IndustrialProps.py is deliberately unchanged so immutable old bundles remain usable.
"""
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
    'industrial_barrel_v3': (.68, .68, 1.1, 1000, 1500),
    'wooden_crate_v2': (1., .8, .8, 600, 1200),
    'concrete_barrier_v2': (2.4, .6, 1., 800, 1200),
    'industrial_fence_v2': (3., .16, 2.2, 1800, 2600),
    'shipping_container_v2': (2.44, 6.06, 2.59, 2200, 3000)}
recipe = req['recipe']
assert recipe in recipes and 0 <= req['seed'] <= 999999
w, d, nominal, minimum, maximum = recipes[recipe]
assert minimum <= req['heightMillimetres'] <= maximum
h = req['heightMillimetres'] / 1000.
w, d = w*h/nominal, d*h/nominal
if recipe == 'industrial_barrel_v3':
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
fine = rng.random((n, n))-.5
# Smooth multiscale fields instead of amplifying uncorrelated single-pixel noise into a lumpy normal map.
def field(layers, start=2):
    result = np.zeros((n, n))
    for k in range(layers):
        f = start*2**k
        result += (np.sin(xx*math.tau*f+rng.uniform(0, math.tau))*
                   np.sin(yy*math.tau*max(1, round(f*.73))+rng.uniform(0, math.tau)))/(2**k)
    return result
cloud = field(6)
detail = field(5, 25)
wear = np.clip((cloud-.65)*1.4, 0, 1)
wood = recipe == 'wooden_crate_v2'
concrete = recipe == 'concrete_barrier_v2'
if wood:
    warp = np.sin(xx*math.tau*1.4)*.10+np.sin(xx*math.tau*4.3)*.025
    lines = np.sin((yy*78+warp)*math.tau)
    fibres = np.sin((yy*212+warp*2.2)*math.tau)
    knots = np.zeros((n, n))
    for cx, cy in [(.28,.32),(.77,.71)]:
        radius = np.sqrt(((xx-cx)/.22)**2+((yy-cy)/.045)**2)
        knots += np.exp(-radius*radius)*(.5+.5*np.sin(radius*18))
    shade = np.clip(.94+lines*.055+fibres*.025+cloud*.055-knots*.21+fine*.025, .50, 1.2)
    colour = np.array([.43, .29, .155])[None, None, :]*shade[:, :, None]
    relief = lines*.004+fibres*.0007+cloud*.0015
elif concrete:
    pores = np.clip((detail-.65)*1.5, 0, 1)
    shade = np.clip(.95+cloud*.04+detail*.025+fine*.04-pores*.06, .65, 1.15)
    colour = np.array([.44, .435, .40])[None, None, :]*shade[:, :, None]
    relief = detail*.002+cloud*.001-pores*.001
else:
    paint = np.array([.23, .32, .25]) if recipe != 'shipping_container_v2' else np.array([.20, .32, .43])
    rust = np.stack([.35+detail*.025, .16+detail*.008, .065+detail*.004], axis=-1)
    colour = paint[None, None, :]*(.95+cloud[:,:,None]*.055+fine[:,:,None]*.02)*(1-wear[:,:,None])+rust*wear[:,:,None]
    relief = wear*.002+detail*.0007
roughness = np.clip(.76+detail*.025 if wood else .82+detail*.025 if concrete else .48+wear*.32+detail*.015, .25, .95)
# Paint and rust are dielectric. Bare metal fittings are a separate material, not an all-over metallic paint coat.
metallic = np.zeros((n, n))
gy, gx = np.gradient(relief)
normals = np.stack([np.clip(-gx*18,-.18,.18), np.clip(-gy*18,-.18,.18), np.ones_like(gx)], axis=-1)
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
surface = dict(schemaVersion=1, recipe=recipe, revision='refined_industrial_finish_v1', resolution=n,
    roughnessMinimum=float(np.min(roughness)), roughnessMaximum=float(np.max(roughness)),
    normalMaximumXY=float(np.max(np.abs(normals[:,:,:2]))), normalMeanZ=float(np.mean(normals[:,:,2])),
    paintedMetallicMaximum=float(np.max(metallic)), proceduralOriginal=True, humanVisualAccepted=False)
with (out/'surface.json').open('x', encoding='utf-8') as stream:
    json.dump(surface, stream, separators=(',', ':'))
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
trim_bsdf.inputs['Base Color'].default_value = (.15, .083, .035, 1) if wood else (.095, .105, .11, 1)
trim_bsdf.inputs['Metallic'].default_value = 0 if wood else .8
trim_bsdf.inputs['Roughness'].default_value = .8 if wood else .5
if wood:
    brace_tex = trim.node_tree.nodes.new('ShaderNodeTexImage')
    brace_tex.image = base
    tint = trim.node_tree.nodes.new('ShaderNodeMixRGB')
    tint.blend_type = 'MULTIPLY'
    tint.inputs[0].default_value = 1
    tint.inputs[2].default_value = (.70, .64, .55, 1)
    trim.node_tree.links.new(brace_tex.outputs['Color'], tint.inputs[1])
    trim.node_tree.links.new(tint.outputs['Color'], trim_bsdf.inputs['Base Color'])
steel = bpy.data.materials.new('Original exposed steel fasteners')
steel.use_nodes = True
steel_bsdf = steel.node_tree.nodes.get('Principled BSDF')
steel_bsdf.inputs['Base Color'].default_value = (.12, .13, .14, 1)
steel_bsdf.inputs['Metallic'].default_value = .85
steel_bsdf.inputs['Roughness'].default_value = .46
marking = bpy.data.materials.new('Original faded safety paint')
marking.use_nodes = True
marking_bsdf = marking.node_tree.nodes.get('Principled BSDF')
marking_bsdf.inputs['Base Color'].default_value = (.48, .31, .035, 1)
marking_bsdf.inputs['Metallic'].default_value = 0
marking_bsdf.inputs['Roughness'].default_value = .78
parts = []

def planar_uv(ob):
    uv = ob.data.uv_layers.active or ob.data.uv_layers.new(name='Metre-scale planar UV')
    for face in ob.data.polygons:
        normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        u_axis, v_axis = [axis for axis in range(3) if axis != normal_axis]
        for loop_index in face.loop_indices:
            co = ob.matrix_world @ ob.data.vertices[ob.data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (co[u_axis]/2, co[v_axis]/2)

def box(name, centre, size, dark=False, bevel=0, finish=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(finish if finish else trim if dark else material)
    if bevel:
        modifier = ob.modifiers.new('Small manufactured edge radius', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if wood:
        # Grain follows each board's long face axis, including vertical/diagonal braces; no random atlas islands.
        uv = ob.data.uv_layers.active or ob.data.uv_layers.new(name='Board grain UV')
        for face in ob.data.polygons:
            normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
            axes = [axis for axis in range(3) if axis != normal_axis]
            u_axis = max(axes, key=lambda axis: size[axis])
            v_axis = next(axis for axis in axes if axis != u_axis)
            for loop_index in face.loop_indices:
                co = ob.data.vertices[ob.data.loops[loop_index].vertex_index].co
                uv.data[loop_index].uv = (co[u_axis]/max(size[u_axis], .001)+.5,
                                          co[v_axis]/max(size[v_axis], .001)+.5)
    else:
        planar_uv(ob)
    parts.append(ob)
    return ob

def cylinder(name, centre, radius, depth, sides, dark=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=centre)
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.append(trim if dark else material)
    for face in ob.data.polygons:
        face.use_smooth = abs(face.normal.z) < .9
    uv = ob.data.uv_layers.active or ob.data.uv_layers.new(name='Seam-continuous cylindrical UV')
    for face in ob.data.polygons:
        points = [ob.data.vertices[ob.data.loops[index].vertex_index].co for index in face.loop_indices]
        if abs(face.normal.z) < .9:
            us = [(math.atan2(co.y, co.x)/math.tau+.5) for co in points]
            if max(us)-min(us)>.5:us=[u+1 if u<.5 else u for u in us]
            coords = [(u, co.z/depth+.5) for u,co in zip(us,points)]
        else:
            coords = [(co.x/(2*radius)+.5, co.y/(2*radius)+.5) for co in points]
        for loop_index, value in zip(face.loop_indices, coords):uv.data[loop_index].uv=value
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
    if recipe == 'industrial_barrel_v3':
        sides = [64, 32, 12][level]
        cylinder('Pressed steel barrel', (0, 0, h*.492), w/2*.975, h*.984, sides)
        for z in [.025, .27, .73, .975]:
            cylinder('Rolled rib', (0, 0, z*h), w/2, .035*h, sides, True)
        if level < 2:
            cylinder('Threaded bung', (w*.25, 0, h*.989), w*.065, h*.022, 16 if level == 0 else 8, True)
        if level == 0:
            cylinder('Vent', (-w*.25, 0, h*.986), w*.028, h*.028, 12, True)
            for z in [.08, .92]:
                cylinder('Fine pressed seam', (0, 0, z*h), w*.492, .008*h, sides, True)
    elif recipe == 'wooden_crate_v2':
        count = [7, 4, 1][level]
        for side in [-1, 1]:
            for i in range(count):
                body_height = h-.11
                z = .055+(i+.5)*body_height/count
                box('Long wooden board', (0, side*(d/2-.055), z), (w-.10, .05, body_height/count*.985), bevel=.003 if level == 0 else 0)
                box('Short wooden board', (side*(w/2-.025), 0, z), (.05, d-.1, body_height/count*.985))
        for i in range(count):
            box('Closed lid board', (0, -d/2+(i+.5)*d/count, h-.025), (w, d/count*.985, .05), bevel=.002 if level==0 else 0)
        box('Crate base', (0, 0, .025), (w, d, .05))
        if level < 2:
            for side in [-1, 1]:
                for x in [-w*.4, w*.4]:
                    box('Corner brace', (x, side*(d/2-.04), h/2), (.07, .08, h-.10), True)
                beam('Diagonal brace', (-w*.40, side*(d/2-.04), h*.12), (w*.40, side*(d/2-.04), h*.88), .045, True)
        if level == 0:
            for side in [-1, 1]:
                for x in [-w*.4, w*.4]:
                    for z in [h*.12, h*.88]:
                        box('Steel fastener head', (x, side*(d/2-.002), z), (.022, .008, .022), finish=steel)
    elif recipe == 'concrete_barrier_v2':
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
        if level == 0:
            bevel = ob.modifiers.new('Subtle cast concrete edges', 'BEVEL')
            bevel.width, bevel.segments = .006*h, 2
            bpy.context.view_layer.objects.active = ob
            ob.select_set(True)
            bpy.ops.object.modifier_apply(modifier=bevel.name)
        planar_uv(ob)
        if level < 2:
            for x in [-w*.35, w*.35]:
                box('Recessed lifting insert', (x, 0, h-.008), (.09, .09, .016), True)
            for side in [-1, 1]:
                for x in [-w*.32, 0, w*.32]:
                    box('Faded lower safety paint', (x, side*(d/2+.0005), h*.135),
                        (w*.19, .001, h*.085), finish=marking)
        if level == 0:
            for x in [-w*.32, w*.32]:
                box('Mould seam', (x, 0, .018), (.012, d, .036), True)
    elif recipe == 'industrial_fence_v2':
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
            for x in [-w*.24, w*.24]:
                box('Separated sealed door leaf', (x, side*(d/2-.015), h/2), (w*.47, .03, h*.94), bevel=.003 if level==0 else 0)
            if level < 2:
                for x in [-w*.30, w*.30]:
                    box('Locking rod', (x, side*(d/2-.008), h/2), (.025, .016, h*.84), True)
        if level == 0:
            for x in [-w*.30, w*.30]:
                for side in [-1, 1]:
                    box('Latch handle', (x, side*(d/2-.006), h*.38), (.18, .012, .035), True)
            for side in [-1, 1]:
                for x in [-w*.46, w*.46]:
                    for z in [.18, .50, .82]:
                        box('Container door hinge', (x, side*(d/2-.01), h*z), (.095, .02, .065), True, bevel=.004)
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
    # Keep the board, world-planar and seam-continuous cylinder mappings authored above.
    # Repacking all joined faces would break continuity and create rust/wood seams.
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
floor_mat.use_nodes = True
floor_bsdf = floor_mat.node_tree.nodes.get('Principled BSDF')
floor_bsdf.inputs['Base Color'].default_value = (.045, .055, .065, 1)
floor_bsdf.inputs['Roughness'].default_value = .85
floor.data.materials.append(floor_mat)
extent = max(w, d, h)
target = Vector((0, 0, h*.48))
for name, location, power, colour in [
    ('Warm key', (extent, -extent, extent*1.8), 550, (1, .88, .73)),
    ('Cool rim', (-extent, extent, extent*1.5), 700, (.70, .84, 1)),
    ('Fill', (-extent, -extent, extent), 200, (1, 1, 1))]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.size, data.color = power*max(1, extent)*.22, extent, colour
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
