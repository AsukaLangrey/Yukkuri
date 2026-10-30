# 本轮低面资源研究

时间：2026-10-31。真实访问作者页面；不用转载站的许可声明代替原作者。所有素材仅作为候选，尚未接入游戏，也未渲染验收。

| 来源 | 已核实内容 | 适用方向 |
|---|---|---|
| [Kenney Nature Kit](https://kenney.nl/assets/nature-kit) | 作者页面 CC0；官方包内 License.txt 标注 Nature Kit 2.1、可商用。实际包有329份GLB，同时有OBJ/MTL、FBX、DAE、STL。 | 小院草丛、河岸花草/石头；优先小配件，避免替换整个既有场景。 |
| [Kenney Food Kit](https://kenney.nl/assets/food-kit) | 作者页面200项、CC0；尚未下载，具体包内格式未核实。 | 丰富地面食物、零食与日后经营道具。 |
| [Quaternius Ultimate Stylized Nature](https://quaternius.com/packs/ultimatestylizednature.html) | 作者页面63模型，FBX/OBJ/Blend/glTF，CC0，含无缝纹理/法线贴图。未下载。 | 比基础几何丰富的树木与植被；作为需远端比较材质一致性的候选。 |
| [Quaternius Ultimate Nature](https://quaternius.com/packs/ultimatenature.html) | 作者页面150模型，FBX/OBJ/Blend，CC0。未下载；不能误写为自带glTF。 | 较朴素的低面自然道具。若选用需在授权远端转换，原机不运行Blender。 |
| [Quaternius Ultimate Food](https://quaternius.com/packs/ultimatefood.html) | 作者页面103模型，FBX/OBJ/Blend，CC0。未下载。 | 食品品类候选，同样先确认远端转换和尺度。 |

## 已下载并静态读取的三个候选

存放：assets-source/night-20261031/candidates/。没有复制到 Game/Assets，没有修改导入或运行加载。

| 文件 | 三角面 | 材质 | 内嵌图片 | SHA256 |
|---|---:|---:|---:|---|
| grass.glb | 132 | 1 | 0 | 260E41D3E5F2472492ED7B475C5B92A30B13CE2BAD408535B5FF50574D4575E7 |
| rock_largeA.glb | 80 | 2 | 0 | 6DD15390FD96501DCD1454765A17BA61DBBD8D47705DFE5149C8DD92B353CE25 |
| flower_yellowA.glb | 76 | 2 | 0 | 8A3B08CD2CA411C21F9C581D5BDA651D78B13C50CBA45AD8FA0389398EAD6D1D |

统计来自GLB JSON的indices访问器，不是运行测量。三个样本不需要外部纹理依赖，适合先在远端比较配色、尺寸和遮挡；未确认实机效果。

官方下载流程需要页面会话及Referer。首次直接下载入口403；随后正常获取作者页面会话并按该页公开下载流程成功。未使用镜像或绕过付费。

原压缩包：kenney-nature-kit.zip，10,537,521字节，SHA256 FA7974A0D342BFE63C38664BA9F8EC1A4AAB8EA25F099BDC56870E33588C4D9D。完整包作为本地来源证据；主协调提交时可只选择三个候选、原许可和此记录，避免把全包当成运行依赖。

保留作者许可 Kenney-Nature-License.txt。CC0 不强制署名，本项目仍保留来源记录。当前优先补油自身表现和实际交互，外部资源用于自然道具，不能把素材下载称为油美术精细度已解决。
