# EC551 scene exporter

This adds the reviewed exporter and old scene to Crayon. The branch builds on `thinh-ti-shading` because the scene bridge uses RTTraversalDemo. The existing Crayon scene and renderer scripts are unchanged.

## Run

Open `Assets/Scenes/EC551Scene.unity`.

- To export: select RTSceneRoot, then use **Export Scene For FPGA** in the RTSceneController component menu. Files go to `RTExport/` in the project root.
- To render with the team code: choose **RTCore > Make Crayon Demo Copy**, then press Play. The menu saves a separate scene, adds RTCore_mesh components, and attaches the traversal demo.

The bridge copies diffuse and emission colors. Mirror/refraction settings are not supported by Crayon's material struct; the bridge reports them as warnings.

## Code

Start with RTSceneObject, RTSceneBuilder, and SceneSerializer under `Assets/RTCore/Scripts/Runtime`. They mark objects, collect world-space geometry, and write files. RTSceneController provides the inspector actions. BVHBuilder groups triangles with median splits. The other files contain the camera, light, material, number conversion, and checking helpers.

The exporter keeps its old binary format. Crayon's FakePGA uses float V0/E1/E2 triangles and does not read this file. The separate FPGA/UART repository was unavailable, so this format is not verified against its receiver.

## RTS1 layout

All multi-byte values are little-endian. Positions use signed Q16.16; camera direction vectors use signed Q4.28. The writer rounds halves away from zero.

Header (32 bytes):

| Offset | Field | Type |
| --- | --- | --- |
| 0 | RTS1 magic | 4 ASCII bytes |
| 4 | Version (1) | u16 |
| 6 | Header size (32) | u16 |
| 8 | Width, height | 2 x u16 |
| 12 | Position fraction bits, camera fraction bits, max bounces, reserved | 4 x u8 |
| 16 | Triangle count | u32 |
| 20 | Material count, light count | 2 x u16 |
| 24 | BVH node count | u32 |
| 28 | Reserved flags | u32 |

Sections follow in this order:

| Section | Record size | Fields |
| --- | --- | --- |
| Camera | 48 bytes | Origin, dir00, dirDX, dirDY; each is 3 x i32 |
| Sky | 8 bytes | Horizon RGB + padding, zenith RGB + padding |
| Lights | 16 bytes each | Position (3 x i32), RGB (3 x u8), Q4.4 intensity (u8) |
| Materials | 16 bytes each | Albedo RGB + pad, emission RGB + pad, reflectivity, transparency, Q2.6 IOR, smoothness, 4 reserved bytes |
| Triangles | 40 bytes each | Three vertices (9 x i32), material ID (u16), flags (u16) |
| BVH nodes | 32 bytes each | Min/max bounds (6 x i32), offset (u32), count (u16), reserved (u16) |
| CRC | 4 bytes | IEEE CRC32 of all preceding bytes |

A triangle's flag bit 0 marks it double-sided. BVH node 0 is the root. An internal node has count 0, with children at offset and offset+1. A leaf refers to count triangles starting at offset. Triangles are stored in BVH leaf order.

The camera ray is `dir00 + x * dirDX + y * dirDY`, with pixel (0,0) at the top left. This differs from FakePGA's bottom-up pixel loop and random ray perturbation.

## Checks

```
python3 Tools/RTCore/rtscene_inspect.py selftest
python3 Tools/RTCore/rtscene_inspect.py check-reference Tools/RTCore/fixtures/reference.rtscene
python3 Tools/RTCore/rtscene_inspect.py check Tools/RTCore/fixtures/scene.rtscene --rays 1000
```

With an untouched old project available, compare the C# outputs too:

```
python3 Tools/RTCore/run_review_checks.py --baseline ~/EC551_Project
```

The runner uses Unity 6000.6.5f1's bundled compiler on macOS; pass `--unity /path/to/Unity.app` for another install.

Validated on Unity 6000.6.5f1:

- Reference and 150 random-scene/leaf-size exports match the old implementation byte for byte.
- Re-exporting the old 598-triangle scene matches the saved original file.
- Original scene BVH vs brute force: 0 mismatches across 1,000 rays.
- Bridged render: 19,200 primary rays; 17,746 hits; 792 shadowed pixels; 862,126 primary triangle tests vs 11,481,600 brute force (92.5% skipped); 0 mismatches across 100 sampled primary rays.
- No compile/runtime exceptions in the integration run. Reflection/transparency warnings are expected.

The sampled Unity check shares the triangle intersection routine between both traversal paths. It checks bounding-box rejection, not an independent proof of the formula. Unity 6000.3.25f1 and the FPGA receiver remain untested.
