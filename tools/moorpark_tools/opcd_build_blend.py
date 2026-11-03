"""Step 2 of the course.svg -> OPCD meshes conversion (step 1: tools/opcd_prep.py). Runs inside Blender 5.2:

    blender -b <tierra-rejada>/TierraRejada_course_svg/TierraRejada_course_svg_1.blend --python tools/opcd_build_blend.py -- build/opcd_plan.pkl build/Moorpark_course_svg.blend

Opens Clender's Tierra Rejada output as the template (its materials, OPCD scene settings and template objects:
bridges, stairs, stakes, walls), removes Tierra's course meshes and builds Moorpark's from the plan, with the same
names, materials, Col colours, "Blend" vertex groups and opcd_* properties as Clender's.
"""
import bpy, pickle, sys
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
PLAN, OUT = argv[0], argv[1]
plan = pickle.load(open(PLAN, "rb"))

for o in [o for o in bpy.data.objects if o.name.startswith(("Spline_", "Spline ", "Gap_Issue"))]:
    bpy.data.objects.remove(o, do_unlink=True)
for me in [m for m in bpy.data.meshes if m.users == 0]:
    bpy.data.meshes.remove(me)
st = bpy.context.scene.opcdtools
st.inner_terrain_size = float(plan["size"])
st.outer_terrain_size = float(plan["size"]) * 2

RED, GREEN, BLUE, BLACK = (1, 0, 0, 1), (0, 1, 0, 1), (0, 0, 1, 1), (0, 0, 0, 1)
PAL = [RED, GREEN, BLUE]
coll = bpy.context.scene.collection


def up_faces(V, faces):
    """wind every face so its normal points +Z"""
    out = []
    for f in faces:
        p = V[list(f)]
        a = 0.5 * np.sum(p[:, 0] * np.roll(p[:, 1], -1) - np.roll(p[:, 0], -1) * p[:, 1])
        out.append(tuple(f) if a > 0 else tuple(reversed(f)))
    return out


def build(o):
    if o["kind"] == "blend":
        inner, outer = np.asarray(o["strip"][0]), np.asarray(o["strip"][1])
        n = len(inner)
        V = np.vstack([inner, outer])
        faces = [(j, (j + 1) % n, n + (j + 1) % n, n + j) for j in range(n)]
        vcol = [RED] * n + [BLACK] * n                    # mesh side red, outline black
    else:
        V = np.asarray(o["verts"])
        faces = o["faces"]
        vcol = [PAL[c] for c in o["col"]]
    faces = up_faces(V, faces)
    me = bpy.data.meshes.new(o["name"])
    me.from_pydata([(float(x), float(y), 0.0) for x, y in V], [], faces)
    me.update()
    ca = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    lv = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", lv)
    cols = np.array(vcol, np.float32)[lv].ravel()
    ca.data.foreach_set("color", cols)
    mat = bpy.data.materials.get(o["material"])
    if mat: me.materials.append(mat)
    ob = bpy.data.objects.new(o["name"], me)
    coll.objects.link(ob)
    ob["opcd_shape_id"] = o["shape_id"]; ob["opcd_parent_id"] = o["parent_id"]; ob["opcd_kind"] = o["kind"]
    if o["kind"] == "blend":
        g = ob.vertex_groups.new(name="Blend")
        g.add(list(range(n, 2 * n)), 1.0, "REPLACE")       # weight 1 on the outline loop
    return ob, len(V)


nv = 0; missing = set()
for i, o in enumerate(plan["objects"]):
    if not bpy.data.materials.get(o["material"]): missing.add(o["material"])
    ob, k = build(o); nv += k
    if i % 200 == 0: print("built", i, "/", len(plan["objects"]), flush=True)
print("BUILT", len(plan["objects"]), "objects,", nv, "vertices; missing materials:", sorted(missing) or "none")
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=False)
print("SAVED", OUT)
