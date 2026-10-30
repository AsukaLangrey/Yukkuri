# Kenney Nature Kit — tree-root decoration subset

- Author: Kenney, https://kenney.nl/
- Original pack: Nature Kit 2.1, https://kenney.nl/assets/nature-kit
- License: CC0 1.0. The original `Kenney-Nature-License.txt` is included unchanged.
- Original acquisition and static metadata: `planning/stages/night-three-lines-20261031/asset-research.md`.
- Copied on 2026-10-31 from `assets-source/night-20261031/candidates/`.

| Runtime file | Bytes | Original SHA256 recorded during acquisition | Original triangle count |
|---|---:|---|---:|
| grass.glb | 11496 | 260E41D3E5F2472492ED7B475C5B92A30B13CE2BAD408535B5FF50574D4575E7 | 132 |
| flower_yellowA.glb | 7120 | 8A3B08CD2CA411C21F9C581D5BDA651D78B13C50CBA45AD8FA0389398EAD6D1D | 76 |

The GLB files were copied byte-for-byte, without conversion, remeshing, texture changes, or material edits. Neither model requires external images. Hashes and triangle counts above are prior static acquisition records, not a new import or runtime validation.

Integration review read the original GLB JSON metadata only: grass mesh bounds span approximately 0.381 × 0.254 × 0.392; flower bounds span 0.159 × 0.1925 × 0.181. Both use identity rotation/scale and a Y translation of -0.05. The loader preserves these aspect ratios, recenters the imported bounds, and uses radius 0.043 × tree scale. Root offsets (±0.17, 0.045) put the center approximately 0.176 × tree scale from the trunk: inner edge approximately 0.133 exceeds the trunk base radius 0.13, while outer edge approximately 0.219 stays inside the existing 0.22 movement exclusion. At that radius the original aspect ratios give short grass approximately 0.040 and flowers approximately 0.069 × tree scale tall. These are static bounds calculations, not a visual acceptance result.

`WoodlandDecorations.cs` loads imported `PackedScene` resources, measures mesh bounds at runtime, and places uniformly scaled copies within existing tree-root movement exclusions. Original GLB mesh and material resources stay unchanged. No new collision, navigation, food, or selectable objects are added. Grass and flower fallback geometry is visibly drawn with a one-time warning per missing/invalid resource; the warning explicitly says the external model did not load.

The repository tracks GLB files using Git LFS. An authorized remote test/build machine must retrieve actual LFS objects and perform normal Godot imports before rendering/export. No `.import` file or `.godot` artifact was generated on the original development machine, and neither model has been run, imported, rendered, or visually accepted there.

The Windows export preset explicitly includes this folder's `.txt` license and `.md` source note alongside the imported resources.

Official API references consulted for this integration:

- https://docs.godotengine.org/en/stable/classes/class_packedscene.html
- https://docs.godotengine.org/en/stable/classes/class_resourceloader.html
- https://docs.godotengine.org/en/stable/classes/class_mesh.html
- https://docs.godotengine.org/en/stable/classes/class_node3d.html
- https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/import_configuration.html
- https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html
