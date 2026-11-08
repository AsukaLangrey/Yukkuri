# 美术交接

结论：23:54 扩权后已修正默认小院勺沿与饭团底面的两处几何接缝，仅改 `InteractionFoodArt.cs`、`InteractionFoodSurface.cs`。修复依据来自源码公式和网格边界，未运行、编译、渲染或预览，实际画面未确认。旧图标/导入文本保持不变，远程缺失素材线索继续保留在 `assets-source/art-polish-20261029.txt`。

## 已修补的默认路径

### 勺沿没有接到勺背边缘

触发入口：默认小院 `InteractionRoom.Tools.cs` 的 FeedMeal 分支调用 `InteractionFoodArt.Spoon`，普通喂饭和拿起地面饭饭都使用这套构造。

`SpoonBowl(1, angle, true)` 的边缘高度为 `.008+.022=.030`；外面为 `.001+.022=.023`。旧 `SpoonRim` 高度 `.028`、`Tube` 半径 `.0035`，最低点为 `.0245`，高于勺背边缘 `.0015`。勺碗内外面都在 radius=1 终止，没有其他侧壁填上这段间隙。

现在 `SpoonRim` 取内外边缘平均位置，高度 `.0265`。原半径仍为 `.0035`，上下极点分别到 `.030` 和 `.023`，内外勺碗与管的角向采样都保留48段。只修连接位置，不改勺柄、食物接触点、勺碗形状或喂食规则。实际可见裂缝程度与修后光照尚未观察，不能把代数关系说成渲染通过。

### 饭团底边分段不一致

触发入口：`InteractionFoodArt.Spoon` 中的手持饭团，以及默认放下饭饭后 `InteractionRoom` 调用 `InteractionFoodArt.Meal` → `Mound` 的盘中饭团。

两者顶面与 `MealEdge` 侧壁均用64段，旧底面却用48段。底面边界与侧壁下缘虽然来自同一个连续函数，但离散成不同多边形；除共同采样点外，线段不重合，留下接缝。两处底面均改为64段，使底面边界沿用侧壁同一组角度。函数、行数、颜色、法线翻转和食物上表面保持原值。每个底面增加256个三角形，没有新增逐帧重建或缓存。

### 静态核对与检查缺口

接手前六份新增授权文件的 Git status 均无差异；逐份读完了 FoodArt/FoodSurface、ToolArt/ToolSurface、WoodlandArt/WoodlandSurface，未发现需立即修改另外四份文件的确定问题。只读核对 `SceneArt.Mat/Mesh`：水、针、石头的专用材料已有 Duplicate，未把它们误报成共享材质污染；不对只读 SceneArt 扩改。

既有 `checks/ActorGeometry.Smoke/Program.cs` 的 “offered spoon and cookie meet the mouth…” 会读取 SpoonRim 并检查它离开脸部；它未运行，且不包含本次勺沿封边/生产饭团采样一致性断言。检查文件不在本任务写权限内，未改断言。建议协调者后续增加：各角度 SpoonRim.Y±.0035 分别等于两层 SpoonBowl 边缘；实际生产底面/侧壁同一角度采样边界一致。旧实现分别会差 .0015、48/64段，不能用只检查连续函数的断言冒充生产网格覆盖。

## 范围与静态依据

- 已读根 AGENTS.md、本轮 PLAN、旧 presentation.md、review-resource-scene.md、美术触摸交接、M01-assets.txt、asset-manifest.json 和 c03 远程报告阅读结论。
- 读取时 HEAD 为 `8238c6e6380fd605ec84cc5267f38de875f6d647`；共享工作区有其他聊天的进行中改动，不能把该 HEAD 当作全部工作区内容身份。
- 默认入口是 `project.godot:7` 的 `InteractionRoom.tscn`；`InteractionRoom.cs:38–39` 只在显式旧模式参数下转到 Main。默认小院没有加载四个分配图标或标题图。
- 图标仅由 `Main.Ui.cs:109–110` 加载；`:46` 的 MilestoneScenario 提前返回又排除了普通 M01 界面。标题图只见于 `Main.Ui.cs:104`、`Main.MilestoneUi.cs:158`。旧 probe GLB 未找到产品加载引用。
- 两个可写 .import 的 source_file 与实际资源路径一致，path 与 dest_files 一致；未见凭文本可确定的路径/依赖错误。现有四个 SVG 及其只读 .import 未发现需要为了默认入口而修改的证据。
- 最初默认美术构造源码不在范围内；23:54 PLAN 追加六份独占后完成上述两份修改。CreatureActor、SceneArt、InteractionRoom 和现有托举保留改动始终只读。
- 旧资源场景审查中的甜甜、河水、隐藏地景问题已有后续整合，未作为本轮新发现重复申报。

## 具体缺口与恢复线索

触发条件：远程工作副本拉不到原始 PNG/GLB 时，素材导入会读取缺失或指针内容，不能因退出码为零就认为导入完整。既有 c03 阅读结论明确记录素材 ERROR 和 blocked，源码身份为 `c6fc7d89795501c26057b991276b4657d62dc122`，实际机器为独立 Debian 云桌面；本轮没有重新访问远端或复现。

本地三个候选原件都在原工作区，文件长度与 HEAD 的 LFS 指针一致：

| 候选文件 | 本地长度 / 指针预期长度 |
| --- | --- |
| src/Yukkuri.Game/Assets/Art/sanctuary-morning.png | 2846992 / 2846992 |
| src/Yukkuri.Game/Assets/pipeline-probe.glb | 98908 / 98908 |
| assets-source/pipeline-probe.blend | 641206 / 641206 |

完整路径、预期 SHA256 及来源见资源记录。这里只读取目录元数据和 Git 指针，未重新计算本地内容哈希，不能据大小宣称原件完全相同或可正常导入。恢复应由负责迁移/统一推送的协调者处理，先确认身份，再让授权远程机器取得原件；不要改 .import 缓存名、关闭资源导入或重生成素材来掩盖缺失。

## 本轮实际写入

产品源码修改：

- `src/Yukkuri.Game/InteractionFoodSurface.cs`：勺沿取勺碗两层边缘中点。
- `src/Yukkuri.Game/InteractionFoodArt.cs`：手持与盘中饭团底面角向采样统一为64段。

新增文档：

- `assets-source/art-polish-20261029.txt`：用途、入口、导入文本和素材恢复线索。
- `planning/stages/core-sprint-20261029-20m/art.md`：本交接。

没有修改 SVG、.import、二进制、生成器、冻结 art03、启动器或他人文件；没有暂存、提交、推送或创建子任务。未新增检查源码，本轮没有检查源码写权限，检查建议和明确缺口见上。

## 远程待测与未完成

以下全部未运行。按 testing/PROTOCOL.md 记录实际机器、最终整合后完整源码提交和未测项：

1. 若协调者补齐素材，远程核对三份原件与对应提交的 LFS 身份，保存导入日志，确认不存在 PNG/GLB 导入 ERROR；不能只记退出码。引擎重写的 .import 差异仅留在测试副本并记录，不自动回写共享源码。
2. 默认启动小院，在普通喂饭和拿地面饭饭两条路径观察勺沿侧面/背面，完成递食后观察空勺；核对盘中饭团底边。远程运行已有勺子避让脸部几何检查，并补核上述边界关系。实际像素、接触和新增底面三角形的性能影响均未测。
3. 只有旧模式也需要验收时，再在已授权远程环境检查其标题图和四个旧框架图标；这不属于默认小院画面改善。
4. 当前角色画风、饰品穿插、手部接触和托举落地观感仍未在本轮确认。下一轮若要直接改善它们，需协调实际美术源码归属；本轮只依据几何接缝做最小修补，没有声称看过新效果。

本任务交回，可由协调者接管分配文件。没有本机运行、编译、测试、渲染、预览、图片生成或 computer-use。
