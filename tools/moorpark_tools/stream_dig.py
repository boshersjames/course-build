"""Blender: the stream channel is too narrow for the OPCD water dig (it found no inner loops), so lower everything
inside its rim 0.35 m; its water plane (terrain - 5 cm, sloping with the stream like Tierra's two-level ponds) then
shows inside the channel.
    blender -b build/Moorpark_course_conformed.blend --python tools/stream_dig.py
"""
import bpy, bmesh
DEPTH = 0.35
for o in bpy.data.objects:
    if not (o.name.startswith("Spline_water_") and o.name.endswith("_Water_Base_Lake_-_Mesh")):
        continue
    me = o.data
    area = sum(p.area for p in me.polygons)
    if area > 1000.0:                     # the lake and the pond (dug metres deep by conform.py); the stream is ~240 m2
        continue
    bm = bmesh.new(); bm.from_mesh(me)
    rim = {v for e in bm.edges if e.is_boundary for v in e.verts}
    dug = 0
    for v in bm.verts:
        if v not in rim:
            v.co.z -= DEPTH; dug += 1
    bm.to_mesh(me); bm.free()
    print("STREAM", o.name, "lowered", dug, "inner verts by", DEPTH)
bpy.ops.wm.save_mainfile()
