"""Assign Tripo jockey PBR textures and re-export cowboy_no_hat.fbx with embedded textures."""
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


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_image(path, colorspace="sRGB"):
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
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
    output.location = (400, 0)
    bsdf.location = (0, 0)
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

    return mat


def find_mesh():
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            return obj
    return None


def assign_material(mesh_obj, material):
    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(material)


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
        for key, path in TEXTURES.items():
            if not os.path.isfile(path):
                raise FileNotFoundError(f"Missing {key}: {path}")

        clear_scene()
        bpy.ops.import_scene.fbx(filepath=IN_FBX)

        armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
        mesh_obj = find_mesh()
        if not mesh_obj:
            raise RuntimeError("No mesh found in cowboy_no_hat.fbx")

        material = make_material()
        assign_material(mesh_obj, material)
        export_fbx(armature, mesh_obj)

        report.update(
            {
                "ok": True,
                "mesh": mesh_obj.name,
                "material": material.name,
                "textures": TEXTURES,
            }
        )
    except Exception as exc:
        report["error"] = str(exc)

    print("BLENDER_EMBED_RESULT=" + json.dumps(report))
    if not report.get("ok"):
        sys.exit(1)


if __name__ == "__main__":
    main()
