"""Inspect loose mesh islands in cowboy_no_hat.fbx."""
import os
import bpy

PROJECT = r"C:\Users\mazen\OneDrive\Desktop\UnityProjects\Horse-Racing"
IN_FBX = os.path.join(PROJECT, "Assets", "TripoModels", "jockey_3d_model", "cowboy_no_hat.fbx")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=IN_FBX)
    mesh_obj = next(o for o in bpy.data.objects if o.type == "MESH")

    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    islands = []
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        bb = obj.bound_box
        xs = [v[0] for v in bb]
        ys = [v[1] for v in bb]
        zs = [v[2] for v in bb]
        cx = sum(xs) / 4.0
        cy = sum(ys) / 4.0
        cz = sum(zs) / 4.0
        islands.append(
            {
                "name": obj.name,
                "verts": len(obj.data.vertices),
                "center": (round(cx, 4), round(cy, 4), round(cz, 4)),
                "size": (
                    round(max(xs) - min(xs), 4),
                    round(max(ys) - min(ys), 4),
                    round(max(zs) - min(zs), 4),
                ),
            }
        )

    islands.sort(key=lambda i: i["verts"], reverse=True)
    for item in islands:
        print(item)


if __name__ == "__main__":
    main()
