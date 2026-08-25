"""Rebuild cowboy_no_hat.fbx from Malbers Rider: keep body, belt, head hair; remove hat/holsters/guns."""
import json
import os
import sys

import bpy

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
OUT_FBX = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model", "cowboy_no_hat.fbx")
COWBOY_TEX_DIR = os.path.join(
    PROJECT,
    "Assets",
    "Malbers Animations",
    "Horse AnimSet Pro",
    "5 - Materials & Textures",
    "Cowboy",
)

# Malbers Rider.FBX loose-part names (see blender_inspect_malbers_cowboy.py).
REMOVE_NAMES = {
    "CowBoy.004",  # hat
    "Bandana",
    "Pistol 01",
    "Pistol 02",
    "CowBoy.001",
    "CowBoy.006",
    "CowBoy.007",
    "CowBoy.010",
    "CowBoy.011",
    "CowBoy.012",
    "CowBoy.013",
}

# Small scalp/hair fill pieces on sides and crown — must stay joined to body.
KEEP_HEAD_NAMES = {"CowBoy.002", "CowBoy.003", "CowBoy.008", "CowBoy.009"}
KEEP_BELT_NAMES = {"CowBoy.005"}

TEXTURES = {
    "basecolor": os.path.join(COWBOY_TEX_DIR, "CowBoyDiffuse.png"),
    "normal": os.path.join(COWBOY_TEX_DIR, "CowBoyNormal.Png"),
    "roughness_metallic": os.path.join(COWBOY_TEX_DIR, "Cowboy_Rough_M.png"),
}


def load_image(path, colorspace="sRGB"):
    image = bpy.data.images.load(path, check_existing=True)
    image.colorspace_settings.name = colorspace
    image.pack()
    return image


def make_material():
    mat = bpy.data.materials.new(name="Cowboy")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    output.location = (500, 0)
    bsdf.location = (100, 0)
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    base = load_image(TEXTURES["basecolor"], "sRGB")
    tex_base = nodes.new("ShaderNodeTexImage")
    tex_base.image = base
    tex_base.location = (-500, 200)
    links.new(tex_base.outputs["Color"], bsdf.inputs["Base Color"])

    normal_img = load_image(TEXTURES["normal"], "Non-Color")
    tex_normal = nodes.new("ShaderNodeTexImage")
    tex_normal.image = normal_img
    tex_normal.location = (-500, -100)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-200, -100)
    links.new(tex_normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    rough_img = load_image(TEXTURES["roughness_metallic"], "Non-Color")
    tex_rough = nodes.new("ShaderNodeTexImage")
    tex_rough.image = rough_img
    tex_rough.location = (-500, -400)
    sep = nodes.new("ShaderNodeSeparateColor")
    sep.location = (-200, -400)
    links.new(tex_rough.outputs["Color"], sep.inputs["Color"])
    links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
    links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])

    return mat


def separate_loose(mesh_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")


def join_meshes(mesh_objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objects[0]
    if len(mesh_objects) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active


def fix_mesh_topology(mesh_obj):
    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    mesh_obj.data.shade_smooth()
    if hasattr(mesh_obj.data, "use_auto_smooth"):
        mesh_obj.data.use_auto_smooth = True
    if hasattr(mesh_obj.data, "auto_smooth_angle"):
        mesh_obj.data.auto_smooth_angle = 0.785398  # 45 deg


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
        path_mode="COPY",
        embed_textures=True,
    )


def main():
    report = {"ok": False, "out": OUT_FBX}
    try:
        for path in TEXTURES.values():
            if not os.path.isfile(path):
                raise FileNotFoundError(path)

        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=RIDER_FBX)

        armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        cowboy = bpy.data.objects.get("CowBoy")
        if not armature or not cowboy:
            raise RuntimeError("Malbers Rider.FBX missing armature or CowBoy mesh")

        separate_loose(cowboy)

        kept = []
        removed = []
        for obj in list(bpy.data.objects):
            if obj.type != "MESH":
                continue
            if obj.name in REMOVE_NAMES:
                removed.append(obj.name)
                bpy.data.objects.remove(obj, do_unlink=True)
            else:
                kept.append(obj.name)

        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        if not meshes:
            raise RuntimeError("No mesh left after cleanup")

        joined = join_meshes(meshes)
        joined.name = "CowBoy"
        fix_mesh_topology(joined)

        material = make_material()
        joined.data.materials.clear()
        joined.data.materials.append(material)

        export_fbx(armature, joined)

        report.update(
            {
                "ok": True,
                "removed": removed,
                "kept_head": [n for n in kept if n in KEEP_HEAD_NAMES],
                "kept_belt": [n for n in kept if n in KEEP_BELT_NAMES],
                "verts": len(joined.data.vertices),
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_REBUILD_COWBOY_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
