# Headless replica of the OPCD Base workflow (OPCD Mesh Editing Tools 3.5.8, default settings):
# Import Terrain -> Conform All -> Bunker depth (All) -> Water depth (All) -> Water planes -> Flatten tees -> Save.
import bpy, bmesh, sys, time, math
from mathutils import Vector
from mathutils.kdtree import KDTree

argv = sys.argv[sys.argv.index('--') + 1:]
OBJ, OUT = argv[0], argv[1]
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:7.1f}s]", *a, flush=True)

# OPCD defaults (properties.py)
TERRAIN_SMOOTH = 1.5
B_LIP, B_INNER, B_DIG, B_SHAPE, B_TYPE = 0.05, 0.04, 1.0, 1.0, 'concave'
W_LIP, W_INNER, W_DIG, W_SHAPE, W_TYPE = 0.05, 2.0, 1.0, 1.0, 'convex'
W_OUTSET = 2.0
TEE_INSET = 2

def activate(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o

# ---------- 1. Import terrain (opcd.convertterrain) ----------
for o in [o for o in bpy.data.objects if o.name == 'Terrain']:
    bpy.data.objects.remove(o)
bpy.ops.wm.obj_import(filepath=OBJ)
terrain = bpy.context.selected_objects[0]
activate(terrain)
terrain.name = terrain.data.name = "Terrain"
m = terrain.modifiers.new("Smooth", 'SMOOTH')
m.use_x, m.use_y, m.use_z, m.factor = False, True, False, TERRAIN_SMOOTH
bpy.ops.object.convert(target='MESH')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
log("terrain", len(terrain.data.vertices), "verts, bbox z",
    min(v.co.z for v in terrain.data.vertices), max(v.co.z for v in terrain.data.vertices))

# ---------- 1b. Close the SVG-validation gap (rough_0_5 vs deep_3, a few cm wide) ----------
from mathutils.geometry import intersect_point_line
def snap_rim(src, dst, tol=0.3):
    """Move src boundary verts onto the nearest point of dst's boundary edges when within tol (XY)."""
    bd = bmesh.new(); bd.from_mesh(dst.data)
    segs = [((dst.matrix_world @ e.verts[0].co).to_2d(), (dst.matrix_world @ e.verts[1].co).to_2d())
            for e in bd.edges if e.is_boundary]
    bd.free()
    bm = bmesh.new(); bm.from_mesh(src.data)
    inv = src.matrix_world.inverted(); moved = 0
    for v in {v for e in bm.edges if e.is_boundary for v in e.verts}:
        w = src.matrix_world @ v.co; p = w.to_2d(); best = None
        for a, b in segs:
            if (a - p).length > 20: continue
            q, t = intersect_point_line(p, a, b); q = a + (b - a) * min(max(t, 0), 1)
            d = (q - p).length
            if best is None or d < best[0]: best = (d, q)
        if best and 1e-4 < best[0] < tol:
            v.co = inv @ Vector((best[1].x, best[1].y, w.z)); moved += 1
    bm.to_mesh(src.data); bm.free()
    return moved
r05, d3 = bpy.data.objects.get('Spline_rough_0_5_Rough_-_Mesh'), bpy.data.objects.get('Spline_deep_3_Deep_-_Mesh')
if r05 and d3:
    log("gap: snapped", snap_rim(d3, r05), "deep_3 rim verts and", snap_rim(r05, d3), "rough_0_5 rim verts")
    ro = bpy.data.objects.get('Spline_Rough_Outer_2_1_piece414_-_Mesh')
    if ro:
        log("gap: snapped", snap_rim(ro, d3, 0.05) + snap_rim(ro, r05, 0.05), "outer-rough rim verts at the 3-way junction")

# ---------- 2. Conform all meshes (opcd.projectmesh, All) ----------
def flatten_and_project(o):
    o.modifiers.clear()
    me = o.data
    co = [0.0] * (len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    co[2::3] = [z - 100 for z in co[2::3]]
    me.vertices.foreach_set('co', co)
    me.update()
    p = o.modifiers.new("Project", 'SHRINKWRAP')
    p.wrap_method = 'PROJECT'; p.project_limit = 0; p.subsurf_levels = 0
    p.cull_face = 'OFF'; p.offset = 0.0; p.use_project_z = True
    p.use_negative_direction = False; p.use_positive_direction = True
    p.target = terrain

def apply_all(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.shade_smooth()
    bpy.ops.object.convert(target='MESH')

conform = [o for o in bpy.data.objects if o.type == 'MESH' and 'Spline' in o.name
           and 'WaterPlane' not in o.name and 'Potwall' not in o.name]
for o in conform: flatten_and_project(o)
apply_all(conform)
missed = {}
for o in conform:
    n = sum(1 for v in o.data.vertices if (o.matrix_world @ v.co).z < -50)
    if n: missed[o.name] = n
log("conformed", len(conform), "meshes; verts that missed the terrain:", missed or "none")

# ---------- edit-mode helpers ----------
def lower_selected(o, d):
    bm = bmesh.from_edit_mesh(o.data)
    for v in bm.verts:
        if v.select: v.co.z -= d
    bmesh.update_edit_mesh(o.data)

def count_sel(o):
    return sum(o.data.count_selected_items())

def dig(o, lip, inner, dig_depth, shape, lip_type, skip_lip_rings):
    """opcd.bunkeredit / opcd.wateredit body after the re-projection."""
    activate(o)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(use_extend=False, use_expand=False, type='VERT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.region_to_loop()
    bpy.ops.mesh.select_all(action='INVERT')
    for _ in range(skip_lip_rings): bpy.ops.mesh.select_less()
    loops = 0
    while count_sel(o) > 0:
        bpy.ops.mesh.select_less(); loops += 1
        if loops > 500: break
    loops = max(loops, 1)
    dig_scaled = dig_depth * 4.0
    scaleX = (10.0 / loops) / shape
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.region_to_loop()
    bpy.ops.mesh.select_all(action='INVERT')
    a, b = (0.6 * inner, 0.4 * inner) if lip_type == 'concave' else (0.4 * inner, 0.6 * inner)
    lower_selected(o, lip)
    bpy.ops.mesh.select_less(); lower_selected(o, a)
    bpy.ops.mesh.select_less(); lower_selected(o, b)
    for i in range(loops):
        bpy.ops.mesh.select_less()
        lower_selected(o, (b * dig_scaled) / ((scaleX * i) + 1))
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    return loops

# ---------- 3. Bunkers (opcd.bunkeredit, All) ----------
bunkers = [o for o in bpy.data.objects if 'Spline' in o.name and 'Bunker' in o.name
           and 'Blend' not in o.name and 'Potwall' not in o.name]
for o in bunkers:
    dig(o, B_LIP, B_INNER, B_DIG, B_SHAPE, B_TYPE, skip_lip_rings=2)
log("dug", len(bunkers), "bunkers")

# ---------- 4. Water bases (opcd.wateredit, All) ----------
lakes = [o for o in bpy.data.objects if 'Spline' in o.name and 'Lake' in o.name and 'Blend' not in o.name]
for o in lakes:
    zb = min(v.co.z for v in o.data.vertices)
    dig(o, W_LIP, W_INNER, W_DIG, W_SHAPE, W_TYPE, skip_lip_rings=0)
    log("  lake", o.name, "bottom", round(zb, 2), "->", round(min(v.co.z for v in o.data.vertices), 2))
log("dug", len(lakes), "water bases")

# ---------- 5. Water planes (opcd.addwaterplane, All) ----------
wmat = bpy.data.materials.get("WaterPlane") or bpy.data.materials.new("WaterPlane")
wmat.diffuse_color = (0, 0, 0.5, 1)
for o in [o for o in lakes if 'Water_Base_Lake' in o.name]:
    me = o.data.copy()
    bm = bmesh.new(); bm.from_mesh(me)
    bverts = {v for e in bm.edges if e.is_boundary for v in e.verts}
    lowest = min(v.co.z for v in bverts) - 0.05
    # outset the rim by W_OUTSET in XY (offset_edges extrude equivalent)
    bedges = [e for e in bm.edges if e.is_boundary]
    dirs = {}
    for e in bedges:
        f = e.link_faces[0]
        a, b = e.verts
        t = (b.co - a.co).to_2d().normalized()
        n = Vector((t.y, -t.x))
        if n.dot((a.co + b.co).to_2d() / 2 - f.calc_center_median().to_2d()) < 0: n = -n
        for v in (a, b): dirs[v] = dirs.get(v, Vector((0, 0))) + n
    ret = bmesh.ops.extrude_edge_only(bm, edges=bedges)
    newv = [g for g in ret['geom'] if isinstance(g, bmesh.types.BMVert)]
    for nv in newv:
        src = [e.other_vert(nv) for e in nv.link_edges if e.other_vert(nv) in dirs]
        if not src: continue
        d = dirs[src[0]]
        if d.length > 1e-9:
            d = d.normalized()
            nv.co.x += d.x * W_OUTSET; nv.co.y += d.y * W_OUTSET
    # Water level follows the LiDAR water surface (terrain - 5 cm) instead of one flat level,
    # because water_1 is two ponds at different heights joined by a channel.
    from mathutils.bvhtree import BVHTree
    tb = BVHTree.FromObject(terrain, bpy.context.evaluated_depsgraph_get())
    for v in bm.verts:
        h = tb.ray_cast(Vector((v.co.x, v.co.y, 1000)), Vector((0, 0, -1)))
        if h[0]: v.co.z = h[0].z - 0.05
    bm.to_mesh(me); bm.free()
    idx = o.name.split('_')[2]
    wp = bpy.data.objects.new(f"Spline water_{idx} WaterPlane", me)
    me.name = wp.name
    me.materials.clear(); me.materials.append(wmat)
    wp.color = (0, 0, 0.5, 1)
    for c in o.users_collection: c.objects.link(wp)
    log("  water plane", wp.name, "z", round(min(v.co.z for v in me.vertices),2), "to", round(max(v.co.z for v in me.vertices),2))

# ---------- 6. Flatten tees (opcd.flattentees, inset 2) ----------
tees = [o for o in bpy.data.objects if 'Spline' in o.name and 'Tee' in o.name
        and 'Blend' not in o.name and o.type == 'MESH']
for o in tees:
    activate(o)
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(o.data)
    bpy.ops.mesh.select_mode(use_extend=False, use_expand=False, type='VERT')
    bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.region_to_loop()
    rim = [v.co.copy() for v in bm.verts if v.select]
    bpy.ops.mesh.select_all(action='INVERT')
    for _ in range(TEE_INSET - 1): bpy.ops.mesh.select_less()
    pad = [v for v in bm.verts if v.select]
    if not pad:
        bpy.ops.object.mode_set(mode='OBJECT'); continue
    bpy.ops.mesh.region_to_loop()
    padrim = [v.co.copy() for v in bm.verts if v.select]
    smooth = min((a - b).length for a in rim for b in padrim) - 0.05
    # transform.resize z=0 about the median point, SMOOTH proportional falloff, connected
    zmed = sum(v.co.z for v in pad) / len(pad)
    padset = set(pad)
    kd = KDTree(len(pad))
    for i, v in enumerate(pad): kd.insert(v.co, i)
    kd.balance()
    for v in bm.verts:
        if v in padset:
            v.co.z = zmed
        elif smooth > 0:
            _, _, dist = kd.find(v.co)
            if dist < smooth:
                t = 1 - dist / smooth
                v.co.z += (zmed - v.co.z) * (3 * t * t - 2 * t * t * t)
    bmesh.update_edit_mesh(o.data)
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
log("flattened", len(tees), "tees")

terrain.hide_set(True)
for g in [o for o in bpy.data.objects if o.name.startswith("Gap_Issue")]: g.hide_set(True); g.hide_render = True   # gap closed above
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=False, copy=False)
log("saved", OUT)
