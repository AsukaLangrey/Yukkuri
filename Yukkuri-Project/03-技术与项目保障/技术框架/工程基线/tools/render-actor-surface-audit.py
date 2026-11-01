"""CPU z-buffer view of exported source surfaces; no engine, desktop or GPU access.
This diagnostic intentionally omits runtime lighting, animation, accessories' small trim,
and other game content. It cannot establish final visual quality or stability.
"""
import json
import math
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

source, output = map(Path, sys.argv[1:3])
data = json.loads(source.read_text(encoding="utf-8"))
width, height, scale = 420, 610, data.get("previewScale", 320)
background = np.array([239, 235, 223], dtype=np.uint8)
light = np.array([-.45, .7, .7]); light /= np.linalg.norm(light)
views = data.get("views", [0, 38, 84])
hop_frames = data.get("hopFrames")
if hop_frames:
    views = [12] * len(hop_frames)
    width, height, scale = 300, 460, 190
canvas = Image.new("RGB", (width * len(views), height + 115), tuple(background))
font_path = Path(__file__).resolve().parents[1] / "src/Yukkuri.Game/Assets/Fonts/NotoSansSC.ttf"
font = ImageFont.truetype(str(font_path), 19)
small = ImageFont.truetype(str(font_path), 14)
for view, yaw in enumerate(views):
    a, b = math.radians(yaw), math.radians(data.get("previewPitch", 8))
    depth_axis = np.array([math.sin(a)*math.cos(b), math.sin(b), math.cos(a)*math.cos(b)])
    right = np.array([math.cos(a), 0, -math.sin(a)])
    up = np.cross(depth_axis, right)
    all_vertices = np.concatenate([np.array(mesh["vertices"]) for mesh in data["meshes"]])
    projected_x, projected_y = all_vertices @ right, all_vertices @ up
    center_x, center_y = (projected_x.min()+projected_x.max())*.5, (projected_y.min()+projected_y.max())*.5
    view_scale = min(scale, (width-44)/max(.01, np.ptp(projected_x)), (height-100)/max(.01, np.ptp(projected_y)))
    if hop_frames:
        # Identical camera/scale in every panel; auto-framing would hide the jump.
        center_x, center_y, view_scale = 0, .34, scale
    pixels = np.broadcast_to(background, (height, width, 3)).copy()
    depth = np.full((height, width), -np.inf)
    if hop_frames:
        floor_y = round(height*.54-(-.46*up[1]-center_y)*view_scale)
        pixels[floor_y:floor_y+2, 18:width-18] = [183, 184, 169]
    for mesh in data["meshes"]:
        vertices = np.array(mesh["vertices"])
        if hop_frames:
            f = hop_frames[view]
            if mesh["name"].startswith("bow "):
                # Match runtime: accessory origin follows the body, cloth stays rigid.
                origin = np.array(data["bowOrigin"])
                vertices += origin*(np.array(f["scale"])-1)
            else:
                vertices *= np.array(f["scale"])
            pitch = math.radians(f["pitch"])
            rotation = np.array([[1,0,0],[0,math.cos(pitch),-math.sin(pitch)],[0,math.sin(pitch),math.cos(pitch)]])
            vertices = vertices @ rotation.T + [0, f["lift"], 0]
        projected = np.column_stack(((vertices @ right-center_x)*view_scale+width/2, height*.54-(vertices @ up-center_y)*view_scale, vertices @ depth_axis))
        color = np.array([int(mesh["color"][n:n+2], 16) for n in (0, 2, 4)])
        for face in mesh["faces"]:
            p = projected[face]; world = vertices[face]
            x0, x1 = max(0, int(np.floor(p[:, 0].min()))), min(width-1, int(np.ceil(p[:, 0].max())))
            y0, y1 = max(0, int(np.floor(p[:, 1].min()))), min(height-1, int(np.ceil(p[:, 1].max())))
            if x1 < x0 or y1 < y0: continue
            area = (p[1, 1]-p[2, 1])*(p[0, 0]-p[2, 0])+(p[2, 0]-p[1, 0])*(p[0, 1]-p[2, 1])
            if abs(area) < 1e-8: continue
            yy, xx = np.mgrid[y0:y1+1, x0:x1+1]; xx = xx+.5; yy = yy+.5
            u = ((p[1, 1]-p[2, 1])*(xx-p[2, 0])+(p[2, 0]-p[1, 0])*(yy-p[2, 1]))/area
            v = ((p[2, 1]-p[0, 1])*(xx-p[2, 0])+(p[0, 0]-p[2, 0])*(yy-p[2, 1]))/area
            w = 1-u-v; z = u*p[0, 2]+v*p[1, 2]+w*p[2, 2]
            region = depth[y0:y1+1, x0:x1+1]
            visible = (u >= 0) & (v >= 0) & (w >= 0) & (z > region)
            if not visible.any(): continue
            normal = np.cross(world[1]-world[0], world[2]-world[0]); normal /= max(np.linalg.norm(normal), 1e-10)
            # Exporters that explicitly preserve Godot's clockwise winding can
            # use directional lighting; legacy partial exports remain two-sided.
            illumination = max(0, -normal @ light) if mesh.get("clockwise", False) else abs(normal @ light)
            shaded = np.clip(color*(.5+.5*illumination), 0, 255).astype(np.uint8)
            pixels[y0:y1+1, x0:x1+1][visible] = shaded; region[visible] = z[visible]
    canvas.paste(Image.fromarray(pixels), (view*width, 55))
draw = ImageDraw.Draw(canvas)
draw.text((24, 12), "短跳关键帧 · 同一镜头 / 同一比例 · 源码离线预览，非游戏截图" if hop_frames else "源码曲面诊断 · 非游戏截图" if len(views)==1 else "源码曲面检查 · 局部模型 / 简化诊断光照 · 非游戏截图", fill="#3f463f", font=font)
for view, yaw in enumerate(views):
    label = ("正面" if yaw == 0 else f"环绕 {yaw}°") + (" · "+data["eyeModel"] if "eyeModel" in data else "")
    if data.get("naturalFacingDegrees", 0):
        label = f"视角 {yaw}° · 朝向 {data['naturalFacingDegrees']:g}°"
    if hop_frames:
        label = ["站立", "蓄力", "起跳", "腾空最高点", "下落", "落地压缩", "恢复"][view] + f"  {hop_frames[view]['age']:.3f}s"
    draw.text((view*width+20, 69), label, fill="#555b52", font=font)
draw.text((24, height+67), "局部曲面关系检查，未纳入完整运行时材质及细节。" if len(views)==1 else "只用于检查曲面间的空间关系；包边、高光、眼眉、运行时材质及完整饰品未全部纳入。", fill="#555b52", font=small)
draw.text((24, height+90), source.name, fill="#777c70", font=small)
output.parent.mkdir(parents=True, exist_ok=True); canvas.save(output)
print(output)
