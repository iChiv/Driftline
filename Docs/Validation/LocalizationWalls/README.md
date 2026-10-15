# 中英文与升降墙修复验证

Unity 2022.3.62f3，Windows 原生 Editor，经 `127.0.0.1:8080/mcp` 检查及构建。

## 改动

- 主菜单新增已保存到 UIRoot Prefab 的语言下拉，可切换中文／English，即时更新并保存本机偏好。菜单、主题、席位、设置、局域网页、五页游戏说明、道具、行动原因及胜负文案均接入翻译。
- 文案表位于 `Assets/Resources/Localization/ChineseEnglish.txt`，可直接编辑；动态数值通过占位符保留。HUD 在刷新数据后重新应用当前语言，避免每次刷新还原中文。切换语言不提交棋局命令或改变网络契约。
- 升降墙生成代码曾先移动机关槽，再用世界坐标辅助函数在原点创建子模型，导致不同格的模型叠在原点并埋入棋盘。现在先在原点组装，再整体移到对应格；五套主题现有资源均已迁移为 GeometryVersion 1。
- 主题墙重新绑定时使用控制器缓存的正常位置及缩放，避免动画途中重新绑定造成墙体缩放错误。模型仍跟随正式规则驱动升降。

## 证据

| 检查 | 结果 | 文件 |
| --- | --- | --- |
| EditMode 全套 | 94/94 | editmode.json |
| PlayMode 全套 | 23/23 | playmode.json |
| 文案补全后定向复查 | 10/10 | catalogue-recheck.json |
| Windows 独立运行 | 158/158 | native-write.jsonl |
| 新进程读取语言偏好 | 3/3 | native-read.jsonl |
| 最终 Windows 构建 | Succeeded，0 errors，0 warnings | build.json |

运行测试覆盖五套主题、四次墙相位切换、动画途中重新绑定和重开对局；墙体位置与规则状态一致，移动模型没有参与静态合批。语言测试通过模拟鼠标操作真实下拉菜单，检查保存、动态 HUD、五页说明、切回中文以及棋局快照不变；测试完成后还原原生输入设备。

Windows 检查另覆盖五套主题两种墙状态、重开对局、英文 HUD 和跨进程语言保存，并在第二次启动还原原有偏好。`native-walls-en.png` 为最终 Windows 包中相机与 Canvas 的离屏渲染；测试窗口隐藏，直接截取未呈现的 backbuffer 会得到黑图，因此显式渲染并检查像素有内容。它与 Editor 截图均已人工查看。

最终增量构建无告警。首次构建仍出现已有的三项资源告警（URP 地形 Shader、Feel 两项默认后处理资源缺脚本），保留于 `initial-build-console.json`；没有将缓存后的零告警解释为这些第三方资源已经修复。

复现入口：`Tools/Validate-Presentation.ps1`。探针只在 Development Build 且带 `--driftline-presentation-smoke` 参数时启动；正常游戏不启动。原有显示设置探针与局域网探针继续保留。

本轮未改规则程序集，与 EnvironmentSettings 包中的 Draftline.Core.dll SHA256 相同；没有重新宣称通过真实两台电脑局域网验收。

## 试玩

`Builds/Driftline-LocalizationWalls-Windows.zip`；完整解压后运行 `Driftline-LocalizationWalls.exe`。主菜单标题下即可选择语言。主题仍为五套，环境仍自动随机。

截图见 `Docs/Review/LocalizationWalls/`。打包文件校验信息见 `package.json`。
