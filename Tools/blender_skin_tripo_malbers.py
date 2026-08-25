"""Skin Tripo jockey mesh onto Malbers R_* skeleton via CowBoy weight transfer."""
import json
import os
import sys

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
RIDER_FBX = os.path.join(
    PROJECT,
    "Assets",
    "Malbers Animations",
    "Horse AnimSet Pro",
    "7 - Models",
    "CowBoy",
    "Rider.FBX",
)
TRIPO_FBX = os.path.join(
    PROJECT,
    "Assets",
    "TripoModels",
    "jockey_3d_model",
    "jockey_3d_model.fbx",
)
OUT_FBX = os.path.join(
    PROJECT,
    "Assets",
    "TripoModels",
    "jockey_3d_model",
    "jockey_malbers_rigged.fbx",
)


def bbox_world(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    mins = Vector((min(corner[i] for corner in corners) for i in range(3)))
    maxs = Vector((max(corner[i] for corner in corners) for i in range(3)))
    return mins, maxs, (mins + maxs) * 0.5, maxs - mins


def find_armature():
    for obj in bpy.data.objects:
        if obj.type == "ARMATURE" and any(b.name == "R_CG" for b in obj.data.bones):
            return obj
    for obj in bpy.data.objects:
        if obj.type == "ARMATURE":
            return obj
    return None


def find_mesh(*names):
    for name in names:
        obj = bpy.data.objects.get(name)
        if obj and obj.type == "MESH":
            return obj
    for obj in bpy.data.objects:
        if obj.type == "MESH" and "tripo" in obj.name.lower():
            return obj
    return None


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    bpy.ops.import_scene.fbx(filepath=path)


def align_tripo_to_cowboy(tripo_mesh, cowboy):
  tripo_mesh.rotation_euler = (1.57079632679, 0.0, 0.0)

  _, _, cow_center, cow_size = bbox_world(cowboy)
  _, _, trip_center, trip_size = bbox_world(tripo_mesh)

  cow_height = max(cow_size.x, cow_size.y, cow_size.z)
  trip_height = max(trip_size.x, trip_size.y, trip_size.z)
  if trip_height > 1e-6:
    uniform_scale = cow_height / trip_height
    tripo_mesh.scale = (uniform_scale, uniform_scale, uniform_scale)

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
  mod.max_distance = 0.0

  bpy.context.view_layer.objects.active = tripo_mesh
  tripo_mesh.select_set(True)
  cowboy.select_set(True)
  bpy.ops.object.modifier_apply(modifier=mod.name)


def transfer_weights_nearest(tripo_mesh, cowboy):
  tripo_mesh.vertex_groups.clear()
  kd = KDTree(len(cowboy.data.vertices))
  for index, vertex in enumerate(cowboy.data.vertices):
    kd.insert(cowboy.matrix_world @ vertex.co, index)
  kd.balance()

  for vertex in tripo_mesh.data.vertices:
    world_co = tripo_mesh.matrix_world @ vertex.co
    _, nearest_index, _ = kd.find(world_co)
    source_vertex = cowboy.data.vertices[nearest_index]
    for group in source_vertex.groups:
      group_name = cowboy.vertex_groups[group.group].name
      target_group = tripo_mesh.vertex_groups.get(group_name)
      if target_group is None:
        target_group = tripo_mesh.vertex_groups.new(name=group_name)
      target_group.add([vertex.index], group.weight, "REPLACE")


def bind_to_armature(tripo_mesh, armature):
    for mod in list(tripo_mesh.modifiers):
        if mod.type == "ARMATURE":
            tripo_mesh.modifiers.remove(mod)

    tripo_mesh.parent = armature
    tripo_mesh.parent_type = "OBJECT"

    arm_mod = tripo_mesh.modifiers.new(name="Armature", type="ARMATURE")
    arm_mod.object = armature
    arm_mod.use_vertex_groups = True


def export_fbx(armature, tripo_mesh):
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    tripo_mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature

    os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
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
            raise RuntimeError(f"Missing Malbers assets arm={armature} cowboy={cowboy}")

        import_fbx(TRIPO_FBX)
        tripo_mesh = find_mesh("tripo_node_13840c59")
        if not tripo_mesh:
            raise RuntimeError("Tripo mesh not found")

        # Remove Tripo armature objects — weights come from CowBoy.
        for obj in list(bpy.data.objects):
            if obj.type == "ARMATURE" and obj != armature:
                bpy.data.objects.remove(obj, do_unlink=True)

        align_tripo_to_cowboy(tripo_mesh, cowboy)
        cowboy_groups = len(cowboy.vertex_groups)
        if cowboy_groups < 10:
            raise RuntimeError(f"CowBoy mesh has no vertex groups: {cowboy_groups}")

        transfer_weights(tripo_mesh, cowboy)
        bind_to_armature(tripo_mesh, armature)

        group_count = len(tripo_mesh.vertex_groups)
        if group_count < 10:
            # Fallback: nearest-vertex group copy.
            transfer_weights_nearest(tripo_mesh, cowboy)
            group_count = len(tripo_mesh.vertex_groups)
        if group_count < 10:
            raise RuntimeError(
                f"Weight transfer produced too few groups: {group_count} (cowboy had {cowboy_groups})"
            )

        export_fbx(armature, tripo_mesh)
        report.update(
            {
                "ok": True,
                "vertex_groups": group_count,
                "tripo_verts": len(tripo_mesh.data.vertices),
                "cowboy_verts": len(cowboy.data.vertices),
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_SKIN_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
