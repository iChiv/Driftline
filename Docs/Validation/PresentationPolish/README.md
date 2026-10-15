# Unity 呈现整理验证

日期：2026-10-15。原生 Windows / Unity 2022.3.62f3 / Windows 侧 MCP。

本轮修改保存的 HUD Prefab、棋盘目标 Prefab 与视觉控制代码；没有修改 Core 规则、AI 策略、地图或网络协议。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| PlayMode 最终回归 | 13/13 通过 | [playmode.json](playmode.json) |
| 编辑器鼠标恢复与新增 UI 点击回归 | 14/14 通过，结束后原生鼠标启用、无虚拟鼠标残留 | [修复记录](INPUT_RECOVERY.md) |
| EditMode 规则／资产回归 | 48/48 通过 | [editmode.json](editmode.json) |
| Game View | 1920×1080、1366×768；四席位与道具布局已人工看图 | [截图](../../Review/PresentationPolish/README.md) |
| 长行动说明 | 40 行合成布局压力文本，正文高度 956，视口上限 280；滚动到底时底边差 0，规则快照未变化 | [scroll-layout.json](scroll-layout.json) |
| Windows Development Build | 成功；最终增量构建 180,993,844 bytes，0 errors / 0 warnings | [build.json](build.json) |
| 独立图形程序启动 | 1280×720，运行 12 秒仍存活，未发现运行异常 | [window-startup.json](window-startup.json) |
| 包完整性 | 302 项，含启动说明、LAN 说明、字体许可证，程序集与已验证构建一致 | [package.json](package.json) |
| 历史 V2 包 | SHA256 未变化 | 同上 |

PlayMode 覆盖五个模型主题的真实鼠标选棋／移动、20 个场景互动、急停、出子、撤销预览、重开局、规则引导和桌面骰子。新增回归确认空卡片不遮挡场景点击、道具卡按库存显隐、悬停反馈不改变规则状态、急停使用独立形状。EditMode 在最后的提示文案和说明卡高度细调之前完成；上述最终 PlayMode 在这些细调之后完成。

测试后的手动 Play Mode 切换曾触发 Unity Test Framework 1.1.33 的 BackgroundWatcher 空引用；详细栈在 [editor-test-runner-note.json](editor-test-runner-note.json)，不来自游戏脚本。场景中缺失脚本数量为 0。独立程序启动另行检查，不将编辑器工具异常计作游戏运行成功或失败。

本轮没有更改网络行为，也没有重复双进程／双机联机验收；既有 V2 的同机结果见 [前轮记录](../BattlefieldsV2/README.md)。第二台 Windows 电脑的真实 LAN 试玩仍待进行。

## 交付

- `Builds/Driftline-Presentation-Windows.zip`：63,449,561 bytes。
- SHA256：`5752D4D8296F5C0BDCFEE6FAF5CC00FBAF27ADE61B301977EC87F0476E8A3C07`。
- 启动文件：`Driftline-Presentation.exe`。
- 可编辑入口：[UI 编辑说明](../../UI_EDITING_GUIDE.md)。

打包命令：

```powershell
./Tools/Package-Battlefields.ps1 -BuildDirectory ./Builds/PresentationPolish -Destination ./Builds/Driftline-Presentation-Windows.zip -LanGuidePath ./Docs/LAN_PLAYTEST.md
```

打包工具保留已有同名归档。若重新打包，指定新的文件名。
