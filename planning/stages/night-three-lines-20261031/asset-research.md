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

## 食品适配复核：继续保留候选，不接运行资源

本次仅读取原 GLB 的 JSON 头与已有源码，未启动模型查看器、导入器、渲染器或验证程序。两个模型都是单节点、单网格、单图元和单材质，节点无额外变换。材质为双面，颜色来自外部 `Textures/colormap.png`；复制时必须保留这一相对依赖。

| 候选 | 原始 POSITION 访问器范围 | 原始宽 × 高 × 深（约） |
|---|---|---|
| rice-ball.glb | X ±0.040600；Y 0–0.130935；Z ±0.057602 | 0.081200 × 0.130935 × 0.115204 |
| cookie.glb | X ±0.109872；Y 0–0.049770；Z ±0.095152 | 0.219744 × 0.049770 × 0.190304 |

这些数值是文件元数据，不是导入后或实机测量。饭团高窄直立，现有正餐却是浅盘饭堆和勺中小份；按现有入口高度等比缩放会明显缩小食物占地，不宜为使用素材而拉伸或摆成多份。饼干形态较接近，但其非圆轮廓、最高点和递送前缘仍需真正网格适配，不能用包围盒最大值代替入口表面。

现有程序饼干已包含较密的贴面拓扑和巧克力碎屑。候选的 68 个三角面只代表较低面数，不足以证明精细度更好。当前自然入口高度（甜食 0.087、正餐 0.107）、手喂前缘以及地面射线包络都与现有几何相连；简单替换会让外观与真实接触不一致。因此两份食品继续留在候选目录，不复制到运行资产，也不新增食品加载系统。已有份额、持物隐藏和自然吃喝流程保持现状，实际观感未验证。

## 交付补查：容器、食水配件与未来小藏身处

核查时间：2026-10-31 07:20–07:21 +08:00（UTC 2026-10-30 23:20）。实际访问以下三个资源包的原作者页面；未下载、购买、导入或预览模型，没有新增运行资源。

| 原作者资源与准确链接 | 当前已核实许可、格式、纹理 | 对本项目的用途和适配限制 |
|---|---|---|
| [Kenney Survival Kit，作者官方 itch 页面](https://kenney-assets.itch.io/survival-kit) | CC0 1.0；70+模型；OBJ、FBX、glTF。官方下载页列包约1.8 MB。页面没有说明纹理是否外置或包含哪些具体容器/帐篷文件，均未确认。 | 作为未来小藏身处/生存容器的首轮目录核对候选，不宣称包内已确认存在可进出的帐篷。先在授权远端核对单件清单与入口形状，再决定是否值得采集。小屋必须另接真实占位、门洞可达、遮挡和实际进出规则，不能只摆外观就称藏身玩法。 |
| [Kay Lousberg：KayKit Mixed Bag 1](https://kaylousberg.itch.io/mixed-bag-1) | 作者页明确CC0；OBJ、FBX、GLTF；使用单张1024×1024渐变图集，可降至128×128。清单明确有水瓶、工具推车、马戏帐篷和灭火器。免费层16+模型，Extra另含完整款式；水瓶/帐篷分别属于哪一层未确认。 | 水瓶可作为未来补水储备容器，工具推车可作用品收纳，小帐篷只作远期藏身外形候选。不能把储水瓶直接替换现有敞口水杯或培养瓶：杯唇接触、水位、瓶口真实孔洞和份额需要独立适配。渐变图集必须随模型保留；封口/可拆部件/内部网格未核实。不购买付费层，也不把页面列出的全部物件当免费已取得。 |
| [Kenney Furniture Kit，原作者主站](https://kenney.nl/assets/furniture-kit) | 页面确认3D、140项、CC0，标签明确家具/桌/椅/床。当前主站未列文件格式与纹理依赖，未下载包核实，因此两项均未确认，不能照搬其它Kenney包的GLB格式。 | 较适合作用品台、低床等静态环境候选，优先级低于前两项。没有证据可将普通柜子当开放容器或小藏身处；床若用于休息，需对齐现有软垫真实支撑而非只换外形。当前不用于替换护理垫、杯口或瓶养实体。 |

访问限制记录：Kenney Survival Kit 主站同名路径本次抓取失败，改读该作者官方 itch 发布页及其下载清单，不据失败断言资源下架。Furniture 的旧作者发布搜索结果提及OBJ/PNG，但与当前140项版本未建立一致性，未据此填写当前格式。KayKit页面版本说明和促销下载栏的金额不一致，本轮不依赖价格作选择。

建议下一步只优先核对 Survival Kit 单件目录、Mixed Bag 1 免费层水瓶的真实文件与纹理；本轮没有确认到比现有程序化护理垫更合适的护理用品模型，不为凑资源强换。上表全部仍是网页研究候选；实际已集成仍仅Nature Kit的grass与flower_yellowA。

## Survival Kit 官方小包实物核对（后续证据）

2026-10-31 07:23–07:25 +08:00，按主协调明确授权，用普通HTTP重新访问[原作者主站](https://kenney.nl/assets/survival-kit)，本次返回200。读取页面公开的“Continue without donating”入口，使用其会话和Referer取得官方下载链接，成功保存原始ZIP；没有登录、支付或绕过访问控制。先前网页抓取失败不能解释成作者资源下架。

本地独立目录：`assets-source/night-20261031/candidates/survival-kit-20261031/`。原ZIP为1,948,174字节，SHA256为`C3586341B5932C87EB43D75D915434F47DAED168B17ED36A03E8CA9977C7443E`。包内许可原文确认Survival Kit 2.0、CC0；目录有80份GLB、80份FBX和80组OBJ/MTL。当前主站写80项，因此无需继续以旧itch页70+作精确数量。

只读ZIP目录/许可/GLB JSON头，确认实际存在bucket、barrel-open、bottle、tent、tent-canvas、tent-canvas-half。六者均为单网格节点、单材质，明确外置图片URI为`Textures/colormap.png`，不再将该包纹理依赖记作未确认。保留三个独立原字节候选：bucket（7,816字节/68三角）、barrel-open（31,772字节/380三角）、tent-canvas-half（15,656字节/136三角），以及7,440字节PNG和715字节原许可。其余模型只留原ZIP内，不扩展采集。

逐文件SHA256、包内路径、原POSITION范围尺寸与采集入口见候选目录的`SOURCE.md`。文件名与元数据证实存在容器/帐篷候选，但不能据此宣称桶内可接水、帐篷门洞可通行或适合现有碰撞；这些仍需远端看实际网格与交互。没有导入/转换/预览/编译/运行检查，不改Game资源或加载。

提交建议：仅将本研究记录和新目录`SOURCE.md`交主线纳入Git；ZIP、GLB、PNG和原许可暂留本地候选证据，不删除，也不默认装进源码交付包。最终文件选择由主协调决定。当前已集成依旧只有Nature Kit草与黄花；本次新增的是已核实到文件的候选，不是第三项运行素材。
