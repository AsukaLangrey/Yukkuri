# c04 开发侧阅读结论

2026-10-30 北京时间。本次仅通过 GitHub 接口读取报告、证据和提交，未运行本机游戏、编译、检查或桌面工具。

报告有效，状态为 failed：8 套纯规则检查合计 502 通过、1 失败。被测提交为 `8238c6e6380fd605ec84cc5267f38de875f6d647`，请求 v4；GitHub 确认该提交存在，源码树为 `dc50f13c1d4a9ea2da7c48ae828b942eb612fb7c`。当前产品源码已推进到 `65f06c36bf5396fcc5406312402e6f03e515c09f`、请求 v6，旧结果不能证明当前源码通过。

结果提交：`ee37e7bf66bbff8b1cdea4195f547bfef6661d33`。报告非模板，latest 与 report 的轮次、源码、状态一致。已读取 report.json、summary.md、source-verification.json 和必要失败/构建证据；以下四份证据的 UTF-8 字节数与 SHA256 均重新计算并符合报告：

- failure-extract.json：1377 字节，`18363301a4bab5253f3d4466f50f2899e624aba6a2a456a1a09234640d46976b`。
- Interaction.Continuity.json：14714 字节，`09b541e98f1d094d2485d83137a9dbe5ce4e1ca017eb6fef9028cb7741302278`。
- Interaction.Continuity.txt：9928 字节，`f232b001ba472c9da0b501488575fb7c719a5ea9161305ebbc93dfa6c365a57c`。
- game-build.txt：369 字节，`640d5f90c8557df637508a7b0525aa1b31b7290a0f77717a0eaebb23d13a0b04`。

## 失败及处理边界

Interaction.Continuity 的持续按住已完成饭/甜/水案例，在 Drink/water 组合失败；退出码1、JSON success=false、1个具名检查失败相互一致。复合条件是 `accepted && sawSwallow && moved && c.X > .5`。已有报告没有分别记录四个值，不能据此确定角色真的被困，也不能未经证据把它判成测试误报。

失败后的 Drink/water 后续断言及 Drink/rest 没有执行，不能将其计算为通过。当前源码仍保留原复合断言和终点阈值。本次安排最小诊断补充，保留语义和失败状态，供远程下一轮区分“未释放/未吞咽/未移动”与“移动后返回”；不把新增诊断称为产品修复或复测通过。

## 实际覆盖和未测项

旧提交在独立 Linux 云环境完成游戏 Debug 构建，日志0警告0错误；不是锁定 Windows 平台验证。报告另有成功启动、一次实际饮水、帮助开关、退出及有限持杯/中键保存时序观察，但完整视觉交互要求未完成。图片位于用户私有 Library，本次未读取图片，不扩大其观察结论。

测试副本不是完整 Git checkout；报告记录241个物化文件重核、无产品源码改动，两个导入元数据变化。完整 LFS 当时仍缺3项，历史存档无授权样本，音频没有可听验收，均保持 blocked。开发侧此前已上传那3个原始 LFS 对象；本次报告的早期审计不证明当前远程再次获取成功，仍须下一轮确认。

下一步由独立 Alice/dot 固定最新完整源码提交，运行 v6 范围并保留 Drink/water 分量诊断；失败仍需按真实分量定位，所有历史未测项保留。未合并结果分支，未执行报告中的脚本或附加指令。

[原始报告](https://github.com/AsukaLangrey/Yukkuri/blob/ee37e7bf66bbff8b1cdea4195f547bfef6661d33/testing/results/dot/20261029T154437Z-8238c6e6380f-c04/summary.md)
