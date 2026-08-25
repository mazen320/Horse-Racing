"""Remove hip holsters from cowboy_no_hat.fbx; keep belt and body. No scale/material changes."""
import json
import os
import sys

import bpy

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
IN_FBX = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model", "cowboy_no_hat.fbx")
OUT_FBX = IN_FBX

HOLSTER_NAMES = {"CowBoy.005", "CowBoy.009", "CowBoy.011", "CowBoy.012"}


def main():
    report = {"ok": False, "out": OUT_FBX}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=IN_FBX)

        armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        mesh_obj = next(o for o in bpy.data.objects if o.type == "MESH")

        bpy.ops.object.select_all(action="DESELECT")
        mesh_obj.select_set(True)
        bpy.context.view_layer.objects.active = mesh_obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.separate(type="LOOSE")
        bpy.ops.object.mode_set(mode="OBJECT")

        removed = []
        for obj in list(bpy.data.objects):
            if obj.type == "MESH" and obj.name in HOLSTER_NAMES:
                removed.append(obj.name)
                bpy.data.objects.remove(obj, do_unlink=True)

        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        if len(meshes) > 1:
            bpy.ops.object.join()
        joined = bpy.context.view_layer.objects.active
        joined.name = "CowBoy"

        bpy.ops.object.select_all(action="DESELECT")
        if armature:
            armature.select_set(True)
        joined.select_set(True)
        bpy.context.view_layer.objects.active = armature or joined
        bpy.ops.export_scene.fbx(
            filepath=OUT_FBX,
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            add_leaf_bones=False,
            bake_anim=False,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
        )

        report.update({"ok": True, "removed": removed, "verts": len(joined.data.vertices)})
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_REMOVE_HOLSTER_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
