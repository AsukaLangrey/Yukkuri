# 本轮低面资源研究

时间：2026-10-31。真实访问作者页面；不用转载站的许可声明代替原作者。草与黄花已在第八批接入运行源码，其余素材仍为候选；均未在本机导入或渲染验收，详见末节。

| 来源 | 已核实内容 | 适用方向 |
|---|---|---|
| [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) | 作者页面 CC0；官方包内 License.txt 标注 Nature Kit 2.1、可商用。实际包有329份GLB，同时有OBJ/MTL、FBX、DAE、STL。 | 小院草丛、河岸花草/石头；优先小配件，避免替换整个既有场景。 |
| [Kenney Food Kit](https://kenney.nl/assets/food-kit) | 官方包内许可为 Food Kit 2.0、CC0；各200份GLB、FBX、OBJ/MTL，含PNG颜色图。只保留两个候选和必要颜色图/许可，不保存完整包。 | 饭团与饼干，供当前食物/零食远端选型。 |
| [Quaternius Ultimate Stylized Nature](https://quaternius.com/packs/ultimatestylizednature.html) | 作者页面63模型，FBX/OBJ/Blend/glTF，CC0，含无缝纹理/法线贴图。未下载。 | 比基础几何丰富的树木与植被；作为需远端比较材质一致性的候选。 |
| [Quaternius Ultimate Nature](https://quaternius.com/packs/ultimatenature.html) | 作者页面150模型，FBX/OBJ/Blend，CC0。未下载；不能误写为自带glTF。 | 较朴素的低面自然道具。若选用需在授权远端转换，原机不运行Blender。 |
| [Quaternius Ultimate Food](https://quaternius.com/packs/ultimatefood.html) | 作者页面103模型，FBX/OBJ/Blend，CC0。未下载。 | 食品品类候选，同样先确认远端转换和尺度。 |

## 已下载并静态读取的三个候选

最初存放：assets-source/night-20261031/candidates/。采集阶段没有复制到 Game/Assets 或修改导入/加载；草与黄花的后续接入见第八批记录。

| 文件 | 三角面 | 材质 | 内嵌图片 | SHA256 |
|---|---:|---:|---:|---|
| grass.glb | 132 | 1 | 0 | 260E41D3E5F2472492ED7B475C5B92A30B13CE2BAD408535B5FF50574D4575E7 |
| rock_largeA.glb | 80 | 2 | 0 | 6DD15390FD96501DCD1454765A17BA61DBBD8D47705DFE5149C8DD92B353CE25 |
| flower_yellowA.glb | 76 | 2 | 0 | 8A3B08CD2CA411C21F9C581D5BDA651D78B13C50CBA45AD8FA0389398EAD6D1D |

统计来自GLB JSON的indices访问器，不是运行测量。三个样本不需要外部纹理依赖，适合先在远端比较配色、尺寸和遮挡；未确认实机效果。

官方下载流程需要页面会话及Referer。首次直接下载入口403；随后正常获取作者页面会话并按该页公开下载流程成功。未使用镜像或绕过付费。

原压缩包：kenney-nature-kit.zip，10,537,521字节，SHA256 FA7974A0D342BFE63C38664BA9F8EC1A4AAB8EA25F099BDC56870E33588C4D9D。完整包作为本地来源证据；主协调提交时可只选择三个候选、原许可和此记录，避免把全包当成运行依赖。

保留作者许可 Kenney-Nature-License.txt。CC0 不强制署名，本项目仍保留来源记录。当前优先补油自身表现和实际交互，外部资源用于自然道具，不能把素材下载称为油美术精细度已解决。

## 食物候选补充（01:44）

2026-10-31通过作者页面的公开免费官方下载流程读取Food Kit 2.0。只从内存中的压缩包提取下列文件，完整食品包没有留在工作区。网页检索工具本次抓取失败，随后作者网页HTTP读取及其正常下载接口成功。

| 候选 | 来源包路径 | 三角面 | 材质 | 字节 | SHA256 |
|---|---|---:|---:|---:|---|
| 饭团 rice-ball.glb | Models/GLB format/rice-ball.glb | 104 | 1 | 6200 | 1C68253D36C0705DA38A63226521BE1F949BD9CE4B7FF8F8529878CD0AAE6E08 |
| 饼干 cookie.glb | Models/GLB format/cookie.glb | 68 | 1 | 7532 | 7FA0D30C679F9ABC9B884AB10509817D9F0AC813DBB1B36041F05118CA50A68A |

二者放在 candidates/，各有一个外部图片引用 `Textures/colormap.png`；已按原相对路径保留这张必要颜色图（512×512、10715字节，SHA256 75BAF74D9483A0FC56AC21CEBA346FBE16FC24A2A1BF0524C53E6BBE802E1153）。不能只复制GLB而丢掉颜色图。原许可 `Kenney-Food-License.txt` 已保留，允许个人、教育与商业使用，署名非强制。

静态选择依据：作者命名明确、低三角面、单材质，分别对应当前正餐与甜食的小型道具需求；没有接入运行资产、没有转换或渲染，实际颜色、比例与手喂遮挡尚未验证。仍以作者来源页 https://kenney.nl/assets/food-kit 与包内原许可为准。

## 第八批：草与黄花进入运行资产

已将 Nature Kit 原候选 grass.glb、flower_yellowA.glb 和原许可复制到 `src/Yukkuri.Game/Assets/ThirdParty/KenneyNature/`，源码位置/加载/降级接线完成；没有本机导入或实机验收。此项更新仅改变这两份自然装饰的集成状态，石头和两份食品仍是未接入候选。

- 目录元数据读取：grass.glb 11496 字节；flower_yellowA.glb 7120 字节；原许可 611 字节。模型没有转换、重建或修改，原 SHA256 沿用上方采集记录，本轮未执行资源检查程序。
- SOURCE.md 随资源保存来源、许可、原采集哈希和无修改声明。Windows 导出配置包含该目录 .txt/.md；GLB 按原 Git LFS 规则保存。
- WoodlandDecorations.cs 在已有树根移动排除范围内加载真实 PackedScene，按网格层级包围盒统一缩放，不改原材质，不新增碰撞/导航/可食用对象。未加载则显式警告并画程序化替代草花，不能把替代画面当成真实 GLB 已验证。
- 已核对官方 [PackedScene](https://docs.godotengine.org/en/stable/classes/class_packedscene.html)、[ResourceLoader](https://docs.godotengine.org/en/stable/classes/class_resourceloader.html)、[Mesh](https://docs.godotengine.org/en/stable/classes/class_mesh.html)、[Node3D](https://docs.godotengine.org/en/stable/classes/class_node3d.html)、[3D 导入配置](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_3d_scenes/import_configuration.html) 与[导出资源过滤](https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html)。当前项目声明 Godot.NET.Sdk 4.7.2；上述稳定文档是接口依据，不表示本机已导入或编译通过。
