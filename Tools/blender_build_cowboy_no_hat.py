"""Build cowboy_no_hat.fbx from Malbers-aligned Tripo mesh: remove holsters, keep belt."""
import json
import os
import sys

import bpy
from mathutils import Vector

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
JOCKEY_DIR = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model")
RIDER_FBX = os.path.join(
    PROJECT,
    "Assets",
    "Malbers Animations",
    "Horse AnimSet Pro",
    "7 - Models",
    "CowBoy",
    "Rider.FBX",
)
TRIPO_FBX = os.path.join(JOCKEY_DIR, "jockey_3d_model.fbx")
OUT_FBX = os.path.join(JOCKEY_DIR, "cowboy_no_hat.fbx")

# Hip holster loose parts from Malbers-weighted Tripo export (not the waist belt).
HOLSTER_NAMES = {"CowBoy.005", "CowBoy.009", "CowBoy.011", "CowBoy.012"}


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    bpy.ops.import_scene.fbx(filepath=path)


def find_armature():
    for obj in bpy.data.objects:
        if obj.type == "ARMATURE":
            return obj
    return None


def find_tripo_mesh():
    for obj in bpy.data.objects:
        if obj.type == "MESH" and "tripo" in obj.name.lower():
            return obj
    return None


def bbox_world(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    mins = Vector((min(c[i] for c in corners) for i in range(3)))
    maxs = Vector((max(c[i] for c in corners) for i in range(3)))
    return mins, maxs, (mins + maxs) * 0.5, maxs - mins


def align_tripo_to_cowboy(tripo_mesh, cowboy):
    tripo_mesh.rotation_euler = (1.57079632679, 0.0, 0.0)
    _, _, cow_center, cow_size = bbox_world(cowboy)
    _, _, trip_center, trip_size = bbox_world(tripo_mesh)
    cow_height = max(cow_size.x, cow_size.y, cow_size.z)
    trip_height = max(trip_size.x, trip_size.y, trip_size.z)
    if trip_height > 1e-6:
        tripo_mesh.scale = (cow_height / trip_height,) * 3
    bpy.context.view_layer.update()
    _, _, trip_center, _ = bbox_world(tripo_mesh)
    tripo_mesh.location = cow_center - trip_center
    tripo_mesh.location.z += cow_size.z * 0.05
    bpy.context.view_layer.update()


def transfer_weights(tripo_mesh, cowboy):
    for mod in list(tripo_mesh.modifiers):
        tripo_mesh.modifiers.remove(mod)
    tripo_mesh.vertex_groups.clear()
    mod = tripo_mesh.modifiers.new(name="WeightTransfer", type="DATA_TRANSFER")
    mod.object = cowboy
    mod.use_vert_data = True
    mod.data_types_verts = {"VGROUP_WEIGHTS"}
    mod.vert_mapping = "NEAREST"
    mod.mix_mode = "REPLACE"
    mod.mix_factor = 1.0
    bpy.context.view_layer.objects.active = tripo_mesh
    tripo_mesh.select_set(True)
    cowboy.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def bind_to_armature(tripo_mesh, armature):
    for mod in list(tripo_mesh.modifiers):
        if mod.type == "ARMATURE":
            tripo_mesh.modifiers.remove(mod)
    tripo_mesh.parent = armature
    arm_mod = tripo_mesh.modifiers.new(name="Armature", type="ARMATURE")
    arm_mod.object = armature
    arm_mod.use_vertex_groups = True


def remove_hat_and_holsters():
    removed = []
    bpy.ops.object.select_all(action="DESELECT")
    for obj in list(bpy.data.objects):
        if obj.type != "MESH" or obj.name == "tripo_node_13840c59":
            continue
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)

    mesh = bpy.data.objects.get("tripo_node_13840c59")
    if not mesh:
        raise RuntimeError("Tripo mesh missing after cleanup")

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    for obj in list(bpy.data.objects):
        if obj.type != "MESH":
            continue
        name = obj.name
        # Remove holsters and hat-ish top pieces; keep belt (CowBoy.004) and body.
        if name in HOLSTER_NAMES or name in {"CowBoy.001", "CowBoy.008", "CowBoy.010"}:
            removed.append(name)
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        # Head/hat remnants on Tripo export
        if name in {"CowBoy.002", "CowBoy.003", "CowBoy.006", "CowBoy.007"}:
            removed.append(name)
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
    return joined, removed


def export_fbx(armature, mesh_obj):
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
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


def main():
    report = {"ok": False, "out": OUT_FBX}
    try:
        clear_scene()
        import_fbx(RIDER_FBX)
        armature = find_armature()
        cowboy = bpy.data.objects.get("CowBoy")
        if not armature or not cowboy:
            raise RuntimeError("Malbers rider assets missing")

        import_fbx(TRIPO_FBX)
        tripo = find_tripo_mesh()
        if not tripo:
            raise RuntimeError("Tripo mesh missing")

        for obj in list(bpy.data.objects):
            if obj.type == "ARMATURE" and obj != armature:
                bpy.data.objects.remove(obj, do_unlink=True)

        align_tripo_to_cowboy(tripo, cowboy)
        transfer_weights(tripo, cowboy)
        bind_to_armature(tripo, armature)

        # Delete Malbers body meshes; keep armature + Tripo skin.
        for obj in list(bpy.data.objects):
            if obj.type == "MESH" and obj != tripo:
                bpy.data.objects.remove(obj, do_unlink=True)

        mesh_obj, removed = remove_hat_and_holsters()
        export_fbx(armature, mesh_obj)

        _, _, _, size = bbox_world(mesh_obj)
        report.update(
            {
                "ok": True,
                "removed": removed,
                "verts": len(mesh_obj.data.vertices),
                "size": tuple(round(s, 3) for s in size),
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_BUILD_COWBOY_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
