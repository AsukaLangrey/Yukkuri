# Kenney Survival Kit 2.0 — local candidates only

Acquired on 2026-10-31, approximately 07:23–07:25 +08:00.

- Author: Kenney, https://kenney.nl/
- Official pack page: https://kenney.nl/assets/survival-kit
- Author's alternate listing: https://kenney-assets.itch.io/survival-kit
- License read from original archive: Survival Kit 2.0, Creative Commons Zero (CC0), personal/educational/commercial use allowed, attribution optional.
- Download used the main page's public “Continue without donating” flow: its published `data-download-id` and `https://kenney.nl/download-link/6173736574732f737572766976616c2d6b6974`, followed by the returned HTTPS URL on kenney.nl. Normal session/Referer only; no account, payment, or access-control bypass. The temporary signed download URL is not retained as a stable source URL.

The original ZIP is retained locally without modification. Only its directory, license text, and selected GLB JSON chunks were read. Three original GLBs and their exact external texture were copied out for potential later selection, without importing, converting, rendering, or running model/project software. No Game/Assets files or runtime loaders reference this directory.

## Files and SHA256

| Local file | Bytes | SHA256 |
|---|---:|---|
| kenney-survival-kit-original.zip | 1948174 | C3586341B5932C87EB43D75D915434F47DAED168B17ED36A03E8CA9977C7443E |
| bucket.glb | 7816 | FF173908480929067CD634D5EDFEDBA038428F1503DB487521492975FA5C8274 |
| barrel-open.glb | 31772 | F24AB1C5AB3685FC758F7E7298BF4F255F039FC6DFC7463711FBE90C4C011A41 |
| tent-canvas-half.glb | 15656 | F6E12C0DE280A9D5E93E27652A3E80EA620374865A6053DBE6FD5FB7EACA9D93 |
| Textures/colormap.png | 7440 | 6CA023BE9F12E8C2358FCF94320C8A894B4C05A3DD0BAD1F5C268312053CACA1 |
| Kenney-Survival-License.txt | 715 | 62C8356876481204FA4D40DC59183DFED777ADF987D7F2A1390FFFE8A699F3FF |

GLBs came from `Models/GLB format/` in the ZIP; the PNG came from that directory's `Textures/colormap.png`. License text is the archive's `License.txt` under a descriptive local name. Archive inventory contains 80 GLB, 80 FBX, and 80 OBJ/MTL model pairs. The author's current main page also says 80 assets; the alternate itch page's “70+” is less precise.

## Static model metadata

All six inspected models have one mesh node with no explicit node transform, one material, and an external image URI `Textures/colormap.png`. The material is double-sided and uses that image as its base colour. GLB does **not** mean self-contained here: keep the PNG relative path. Values below come from POSITION accessor bounds and index counts, not an imported or rendered measurement.

| Model | Triangles | Approximate width × height × depth | Retained outside ZIP |
|---|---:|---|---|
| bucket.glb | 68 | .144125 × .192063 × .144125 | Yes |
| barrel-open.glb | 380 | .236500 × .344000 × .236500 | Yes |
| bottle.glb | 96 | .052689 × .153869 × .060840 | No |
| tent.glb | 124 | .560762 × .491368 × .561000 | No |
| tent-canvas.glb | 148 | .560762 × .491368 × .561000 | No |
| tent-canvas-half.glb | 136 | .560762 × .491368 × .561000 | Yes |

The bucket/open barrel are concrete container candidates, and the tent variants are concrete shelter appearance candidates. Their names and JSON bounds do not establish that the interiors/entrances are navigable or that a rim matches the existing mouth-contact geometry. No mesh vertices or triangles were executed or rendered for this review. Existing cup lips, liquid levels, portion sources, bottle apertures, obstacle footprints, and creature support must remain authoritative in any later adaptation. A shelter would also need actual enter/leave and occupancy rules; a decorative tent alone is not that feature.

## Commit/package intent

Recommend committing this readable source/metadata list and the stage's `asset-research.md` only. Keep the original ZIP, three GLBs, PNG, and original license locally for evidence/possible selection; do not add them to the delivered runtime/source asset bundle merely because they were downloaded. Root owns the final Git/package selection. Do not delete the retained original archive. If an asset is selected later, include its license and required PNG together and recheck its real geometry on the authorized remote machine.
