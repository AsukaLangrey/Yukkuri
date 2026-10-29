# 用户可视复演：partial

**这是旧构建的有限可见复演，不是完整复测或验收通过。** 用户于 16:25:42 UTC 要求“你再测试一轮我看”。本轮仅在自己的 Alice 云桌面复用已核对的 `8238c6e` 构建，使用新隔离 profile；未在用户原开发机运行。

- 本轮：`20261029T162613Z-8238c6e6380f-v05`；关联旧报告：`20261029T154437Z-8238c6e6380f-c04`（retry_of；不覆盖旧报告）。
- 被测源码：`8238c6e6380fd605ec84cc5267f38de875f6d647`；树：`dc50f13c1d4a9ea2da7c48ae828b942eb612fb7c`。
- 开始：2026-10-29T16:26:13Z；结束：2026-10-29T17:12:31.308700+00:00。
- **当前已知 main：`c400a4c5eedc236d779b1e28142b5c8f6ca5014d`；本报告不覆盖该新提交。**
- 239 个非导入元数据源文件和已构建 DLL 身份重核；**没有重新编译、没有运行八套规则检查**。

## 实际观察

用户要求旧8238c6e可视复演。16:28:08实际启动，云桌面前景庭院确认；点击近看后实际观察到角色近景。16:29:21起因用户可能正在操作且未确认，暂停所有鼠标键盘输入并保持窗口。没有执行本轮饮水/帮助/回小院序列，不把c04旧观察充数；也未独立确认用户端确实看见。经主对话确认结束展示后，17:11:30只关闭本次游戏，正常退出0；没有再做玩法交互。

完整操作、预期/实际、时长及未覆盖范围见 `evidence/native-observation.json`（如尚未提供则为待补草稿）。用户是否实际看到每帧未独立确认，不把云窗口可见冒充用户端已验收。

## 本轮检查

| 检查 | 状态 | 本轮通过 / 失败 |
|---|---|---:|
| lfs-integrity | not_run | 未计数 |
| game-build | not_run | 未计数 |
| Simulation.Smoke | not_run | 未计数 |
| Milestone.Smoke | not_run | 未计数 |
| Interaction.Smoke | not_run | 未计数 |
| Input.Intent | not_run | 未计数 |
| NaturalLife.Smoke | not_run | 未计数 |
| Interaction.Continuity | not_run | 未计数 |
| ActorGeometry.Smoke | not_run | 未计数 |
| NaturalLife.Soak | not_run | 未计数 |
| Save.Compatibility | not_run | 未计数 |
| runtime-smoke | passed | 1 / 0 |
| visual-interaction-review | not_run | 未计数 |
| native-observed-interaction | passed | 未计数 |

## 历史问题仍保留

c04 的 `Interaction.Continuity` 在第143行的复合断言有1项失败，根因未确认。本轮没有修复源码，也没有重跑该确定性检查；不能用一次可见运行消除旧失败。c04 的 **502通过/1失败不计入本轮**。

素材缺口、历史存档样本、有效音频及全部未观察 UI 场景并未被此次复演解决。完整 visual-interaction-review 仍未执行；项目整体验收由用户决定。

## 下一轮与 latest 指针

本报告标为 `manual_visible_replay`，`eligible_as_full_test_baseline=false`。更新 latest 仅表示最新人工复演报告，不表示新 main 已测试。下一轮仍须基于上次完整规则轮次 c04 比较关注路径；`f02b0fc` 及后续新提交不得因此被去重跳过或标为通过。

普通 Git 只保留脱敏文本/JSON；任何私有图片存储若提供，权限、身份与有效期以本轮 evidence 元数据为准。
