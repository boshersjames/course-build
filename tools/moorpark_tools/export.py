# opcd.batch_fbx_export with selection 'All': one FBX per visible mesh, origin at world 0, bake_space_transform.
import bpy, os, sys
out = sys.argv[sys.argv.index('--') + 1]
os.makedirs(out, exist_ok=True)
bpy.context.scene.cursor.location = (0, 0, 0)
objs = [o for o in bpy.context.visible_objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.object.origin_set(type='ORIGIN_CURSOR', center='MEDIAN')
bpy.ops.object.select_all(action='DESELECT')
for o in objs:
    o.select_set(True); bpy.context.view_layer.objects.active = o
    fn = os.path.join(out, bpy.path.clean_name(o.name) + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fn, use_selection=True, bake_space_transform=True)
    o.select_set(False)
print("EXPORTED", len(objs), "FBX files to", out)
