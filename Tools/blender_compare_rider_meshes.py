"""Report bounds of key rider meshes in Unity import units."""
import os
import bpy

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
PATHS = {
    "malbers_rider": os.path.join(
        PROJECT,
        "Assets",
        "Malbers Animations",
        "Horse AnimSet Pro",
        "7 - Models",
        "CowBoy",
        "Rider.FBX",
    ),
    "cowboy_no_hat": os.path.join(
        PROJECT, "Assets", "TripoModels", "jockey_3d_model", "cowboy_no_hat.fbx"
    ),
    "jockey_malbers_rigged": os.path.join(
        PROJECT, "Assets", "TripoModels", "jockey_3d_model", "jockey_malbers_rigged.fbx"
    ),
}


def mesh_bounds(obj):
    bb = obj.bound_box
    xs = [v[0] for v in bb]
    ys = [v[1] for v in bb]
    zs = [v[2] for v in bb]
    return {
        "name": obj.name,
        "verts": len(obj.data.vertices),
        "size": tuple(round(max(xs) - min(xs), 4) for _ in [0])  # placeholder
    }


def report(path, label):
    if not os.path.isfile(path):
        print(label, "MISSING", path)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    print(f"=== {label} ({len(meshes)} meshes) ===")
    for obj in meshes:
        bb = obj.bound_box
        xs = [v[0] for v in bb]
        ys = [v[1] for v in bb]
        zs = [v[2] for v in bb]
        print(
            {
                "name": obj.name,
                "verts": len(obj.data.vertices),
                "center": (round(sum(xs) / 4, 3), round(sum(ys) / 4, 3), round(sum(zs) / 4, 3)),
                "size": (
                    round(max(xs) - min(xs), 3),
                    round(max(ys) - min(ys), 3),
                    round(max(zs) - min(zs), 3),
                ),
            }
        )


def main():
    for key, path in PATHS.items():
        report(path, key)


if __name__ == "__main__":
    main()
