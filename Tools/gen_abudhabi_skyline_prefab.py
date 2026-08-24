import math

buildings = [
    ("AbuD_bld07", "713fe1be909fc734eb7314dec1aa80a4", 2148255665486586522, 1629163463429077024, 0, 0, 820, 0, 2.2),
    ("AbuD_bld06", "a612a9839ddd10f4ab58cab142422fef", 2568161252910774070, 2947642932009332108, -520, 0, 680, 30, 1.8),
    ("AbuD_bld05", "2d4bb4dae0c775442aa0d52dde3d9381", 2320120619501110602, 3132651313525694448, 520, 0, 680, -30, 1.8),
    ("AbuD_bld03", "ecb15262c3dd1eb4181ff5a601282735", 6306317375378063568, 6687663550535096938, -820, 0, 450, 45, 1.6),
    ("AbuD_bld04", "b7c9a7a0f90f52045bdaf9f177ff9911", 407576034525839390, 1072930465726356132, 820, 0, 450, -45, 1.6),
    ("AbuD_bld02", "d85f61d66d38b214a8cd46d23e3c38a7", 1105043284242782695, 296735853299401565, -300, 0, 720, 15, 1.7),
    ("AbuD_bld01", "5df496c35e4a7914286c2a748977cfad", 2234284708806334610, 1462006001078977064, 300, 0, 720, -15, 2.0),
]


def quat_y(deg):
    r = math.radians(deg) / 2.0
    return 0, math.sin(r), 0, math.cos(r)


def prefab_instance(inst_id, strip_id, parent_id, name, guid, tid, gid, x, y, z, ry, scale):
    qx, qy, qz, qw = quat_y(ry)
    block = [
        f"--- !u!1001 &{inst_id}",
        "PrefabInstance:",
        "  m_ObjectHideFlags: 0",
        "  serializedVersion: 2",
        "  m_Modification:",
        "    serializedVersion: 3",
        f"    m_TransformParent: {{fileID: {parent_id}}}",
        "    m_Modifications:",
    ]
    for prop, val in [
        ("m_LocalPosition.x", x),
        ("m_LocalPosition.y", y),
        ("m_LocalPosition.z", z),
        ("m_LocalRotation.w", qw),
        ("m_LocalRotation.x", qx),
        ("m_LocalRotation.y", qy),
        ("m_LocalRotation.z", qz),
        ("m_LocalEulerAnglesHint.x", 0),
        ("m_LocalEulerAnglesHint.y", ry),
        ("m_LocalEulerAnglesHint.z", 0),
        ("m_LocalScale.x", scale),
        ("m_LocalScale.y", scale),
        ("m_LocalScale.z", scale),
    ]:
        block.extend([
            f"    - target: {{fileID: {tid}, guid: {guid}, type: 3}}",
            f"      propertyPath: {prop}",
            f"      value: {val}",
            "      objectReference: {fileID: 0}",
        ])
    block.extend([
        f"    - target: {{fileID: {gid}, guid: {guid}, type: 3}}",
        "      propertyPath: m_Name",
        f"      value: {name}",
        "      objectReference: {fileID: 0}",
        "    m_RemovedComponents: []",
        "    m_RemovedGameObjects: []",
        "    m_AddedGameObjects: []",
        "    m_AddedComponents: []",
        f"  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}",
        f"--- !u!4 &{strip_id} stripped",
        "Transform:",
        f"  m_CorrespondingSourceObject: {{fileID: {tid}, guid: {guid}, type: 3}}",
        f"  m_PrefabInstance: {{fileID: {inst_id}}}",
        "  m_PrefabAsset: {fileID: 0}",
    ])
    return block


child_ids = []
instances = []
inst_id = 9002000000000000000
strip_id = 9003000000000000000
for b in buildings:
    inst_id += 1
    strip_id += 1
    child_ids.append(strip_id)
    instances.extend(prefab_instance(inst_id, strip_id, 9001000000000000002, *b))

lines = [
    "%YAML 1.1",
    "%TAG !u! tag:unity3d.com,2011:",
    "--- !u!1 &9001000000000000001",
    "GameObject:",
    "  m_ObjectHideFlags: 0",
    "  m_CorrespondingSourceObject: {fileID: 0}",
    "  m_PrefabInstance: {fileID: 0}",
    "  m_PrefabAsset: {fileID: 0}",
    "  serializedVersion: 6",
    "  m_Component:",
    "  - component: {fileID: 9001000000000000002}",
    "  m_Layer: 0",
    "  m_Name: AbuDhabi_Skyline",
    "  m_TagString: Untagged",
    "  m_Icon: {fileID: 0}",
    "  m_NavMeshLayer: 0",
    "  m_StaticEditorFlags: 2147483647",
    "  m_IsActive: 1",
    "--- !u!4 &9001000000000000002",
    "Transform:",
    "  m_ObjectHideFlags: 0",
    "  m_CorrespondingSourceObject: {fileID: 0}",
    "  m_PrefabInstance: {fileID: 0}",
    "  m_PrefabAsset: {fileID: 0}",
    "  m_GameObject: {fileID: 9001000000000000001}",
    "  serializedVersion: 2",
    "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
    "  m_LocalPosition: {x: 0, y: 0, z: 0}",
    "  m_LocalScale: {x: 1, y: 1, z: 1}",
    "  m_ConstrainProportionsScale: 0",
    "  m_Children:",
]
lines.extend(f"  - {{fileID: {cid}}}" for cid in child_ids)
lines.extend([
    "  m_Father: {fileID: 0}",
    "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
])
lines.extend(instances)

path = r"C:\Users\User\Desktop\Projects\Horse-Racing\Assets\Contents\Environment\AbuDhabi\AbuDhabi_Skyline.prefab"
with open(path, "w", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
print("Wrote", path, "lines", len(lines))
