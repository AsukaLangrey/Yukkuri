"""Original geometric asset to verify Blender -> GLB -> Godot; not game artwork."""
import bpy
from pathlib import Path

root = Path(__file__).resolve().parent.parent
destination = root / "src" / "Yukkuri.Game" / "Assets"
destination.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    return mat

cream = material("Probe cream", (0.9, 0.74, 0.48))
dark = material("Probe eyes", (0.025, 0.035, 0.045))
red = material("Probe cap", (0.56, 0.12, 0.15))
def sphere(name, location, scale, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for poly in obj.data.polygons:
        poly.use_smooth = True

sphere("Pipeline probe body", (0, 0, 0.62), (0.7, 0.58, 0.62), cream)
sphere("Left eye", (-0.23, -0.54, 0.69), (0.07, 0.045, 0.09), dark)
sphere("Right eye", (0.23, -0.54, 0.69), (0.07, 0.045, 0.09), dark)
sphere("Cap", (0, 0, 1.14), (0.65, 0.55, 0.12), red)
source = root / "assets-source"
source.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source / "pipeline-probe.blend"))
bpy.ops.export_scene.gltf(filepath=str(destination / "pipeline-probe.glb"), export_format='GLB')
print("BLENDER_PIPELINE_OK", destination)
