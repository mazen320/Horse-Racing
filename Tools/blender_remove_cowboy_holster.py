"""Remove hip holster islands from cowboy_no_hat.fbx, keep belt, re-embed textures."""
import json
import os
import sys

import bpy

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
JOCKEY_DIR = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model")
FBM_DIR = os.path.join(JOCKEY_DIR, "jockey_3d_model.fbm")
IN_FBX = os.path.join(JOCKEY_DIR, "cowboy_no_hat.fbx")
OUT_FBX = IN_FBX

TEXTURES = {
    "basecolor": os.path.join(FBM_DIR, "jockey_3d_model_basecolor.JPEG"),
    "normal": os.path.join(FBM_DIR, "jockey_3d_model_normal.JPEG"),
}

# Loose parts identified on 2026-08-26 — lateral hip gun holsters; belt is CowBoy.004 (center x~0).
HOLSTER_OBJECT_NAMES = {
    "CowBoy.005",
    "CowBoy.011",
    "CowBoy.009",
    "CowBoy.012",
}


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_image(path, colorspace="sRGB"):
    image = bpy.data.images.load(path, check_existing=True)
    image.colorspace_settings.name = colorspace
    return image


def make_material():
    mat = bpy.data.materials.new(name="jockey_3d_model")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    base = load_image(TEXTURES["basecolor"], "sRGB")
    tex_base = nodes.new("ShaderNodeTexImage")
    tex_base.image = base
    links.new(tex_base.outputs["Color"], bsdf.inputs["Base Color"])

    normal_img = load_image(TEXTURES["normal"], "Non-Color")
    tex_normal = nodes.new("ShaderNodeTexImage")
    tex_normal.image = normal_img
    normal_map = nodes.new("ShaderNodeNormalMap")
    links.new(tex_normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    return mat


def separate_loose(mesh_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")


def is_holster(obj):
    if obj.name in HOLSTER_OBJECT_NAMES:
        return True

    if obj.type != "MESH":
        return False

    bb = obj.bound_box
    xs = [v[0] for v in bb]
    zs = [v[2] for v in bb]
    cx = sum(xs) / 4.0
    cz = sum(zs) / 4.0
    width_x = max(xs) - min(xs)

    verts = len(obj.data.vertices)
    if verts >= 50:
        return False
    if abs(cx) < 12.0:
        return False
    if width_x > 12.0:
        return False
    if not (30.0 <= cz <= 75.0):
        return False
    return True


def remove_holsters():
    removed = []
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and is_holster(obj):
            removed.append(obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
    return removed


def join_remaining_meshes():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("No mesh left after holster removal")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = "CowBoy"
    return joined


def export_fbx(armature, mesh_obj):
    bpy.ops.object.select_all(action="DESELECT")
    if armature:
        armature.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature or mesh_obj

    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
    )


def main():
    report = {"ok": False, "out": OUT_FBX}
    try:
        clear_scene()
        bpy.ops.import_scene.fbx(filepath=IN_FBX)

        armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        mesh_obj = next(o for o in bpy.data.objects if o.type == "MESH")
        separate_loose(mesh_obj)
        removed = remove_holsters()
        mesh_obj = join_remaining_meshes()

        material = make_material()
        mesh_obj.data.materials.clear()
        mesh_obj.data.materials.append(material)
        export_fbx(armature, mesh_obj)

        report.update(
            {
                "ok": True,
                "removed": removed,
                "verts": len(mesh_obj.data.vertices),
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_REMOVE_HOLSTER_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
