"""Convert jockey helmet GLB -> FBX for Unity."""
import bpy
import sys
from pathlib import Path

argv = sys.argv
argv = argv[argv.index("--") + 1 :] if "--" in argv else []
src = Path(argv[0])
dst = Path(argv[1])

# Clear default scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for block in bpy.data.meshes:
    bpy.data.meshes.remove(block)

bpy.ops.import_scene.gltf(filepath=str(src))

# Apply scales / make single root friendly for Unity
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# Rename mesh objects
for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        obj.name = "JockeyHelmet"
        if obj.data:
            obj.data.name = "JockeyHelmet"

dst.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=str(dst),
    use_selection=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    path_mode="COPY",
    embed_textures=True,
)
print("EXPORTED", dst, "exists=", dst.exists(), "bytes=", dst.stat().st_size if dst.exists() else 0)
