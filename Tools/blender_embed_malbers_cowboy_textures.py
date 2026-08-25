"""Assign Malbers Cowboy textures and re-export cowboy_no_hat.fbx with embedded textures."""
import json
import os
import sys

import bpy

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
COWBOY_TEX_DIR = os.path.join(
    PROJECT,
    "Assets",
    "Malbers Animations",
    "Horse AnimSet Pro",
    "5 - Materials & Textures",
    "Cowboy",
)
IN_FBX = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model", "cowboy_no_hat.fbx")
OUT_FBX = IN_FBX

TEXTURES = {
    "basecolor": os.path.join(COWBOY_TEX_DIR, "CowBoyDiffuse.png"),
    "normal": os.path.join(COWBOY_TEX_DIR, "CowBoyNormal.Png"),
    "roughness_metallic": os.path.join(COWBOY_TEX_DIR, "Cowboy_Rough_M.png"),
}


def load_image(path, colorspace="sRGB"):
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
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


def find_meshes():
    return [o for o in bpy.data.objects if o.type == "MESH"]


def assign_material(mesh_objects, material):
    for mesh_obj in mesh_objects:
        mesh_obj.data.materials.clear()
        mesh_obj.data.materials.append(material)


def export_fbx(armature, mesh_objects):
    bpy.ops.object.select_all(action="DESELECT")
    if armature:
        armature.select_set(True)
    for mesh_obj in mesh_objects:
        mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature or mesh_objects[0]

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
        for key, path in TEXTURES.items():
            if not os.path.isfile(path):
                raise FileNotFoundError(f"Missing {key}: {path}")

        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=IN_FBX)

        armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        mesh_objects = find_meshes()
        if not mesh_objects:
            raise RuntimeError("No mesh found in cowboy_no_hat.fbx")

        material = make_material()
        assign_material(mesh_objects, material)
        export_fbx(armature, mesh_objects)

        report.update(
            {
                "ok": True,
                "meshes": [o.name for o in mesh_objects],
                "material": material.name,
                "textures": TEXTURES,
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_EMBED_MALBERS_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
