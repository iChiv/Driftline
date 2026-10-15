# 简洁 UI 与菜单观察镜头验证

2026-10-15，Unity 2022.3.62f3，Windows 本地 Editor / MCP。

## 本轮结果

- 主菜单保留 DRIFTLINE 标题、五种主题与简短功能入口。移除故事、操作提示和按钮后缀。
- 红、蓝、绿、黄方不再使用故事名称。对局保留必要回合、滑行、生命、场上数量与道具状态；隐藏目标卡、事件日志、底部常驻说明。预览按悬停显示，吃子或无路可走的原因停留 5 秒。
- “游戏说明”首页只显示胜利条件。操作、生命、传送与道具放在后续四页，按需翻阅。
- 圆角使用保存的九宫格 Sprite，布局仍在 UIRoot Prefab 中；按钮、输入框、下拉列表、面板和状态卡已应用。展开项采用实色背景，避免透出后方按钮。
- 菜单使用三个观察机位，默认每个机位停留 9 秒、过渡 3.5 秒并轻微漂移。配置、设置、说明及悬停机关时暂停轮巡；对局禁用全部菜单机位，使用原稳定操作镜头。
- 五套主题中隐藏骰台提示与装饰文字，保留出生点和传送门配对符号。

## 验证

- [EditMode](editmode.json)：67/67 通过，包含上一轮生命与传送门规则。
- [PlayMode](playmode.json)：18/18 通过。包含菜单轮巡、说明暂停轮巡、进入对局后镜头稳定、真实合成鼠标展开与选择下拉项、菜单开局、五主题棋盘点击、急停、部署、骰子、传送门堵塞、滚轮与规则说明。
- [原生输入恢复](input-after-tests.json)：鼠标与键盘启用，当前鼠标为原生设备，总计两个原生设备；Editor 已退出 Play Mode。测试修改的 EditorSettings / ProjectSettings 已恢复。
- [画面检查](../../Review/MinimalUI/README.md)：1366×768 菜单 / 配置 / 游戏说明，1280×720 下拉列表与四方对局。
- [Windows 构建](build.json)：成功，0 errors，3 warnings。警告与之前一致：URP TerrainDetail D3D11 shader 一条，Feel 后处理资源缺失脚本两条；详见 [Editor 构建消息](editor-build-messages.json)。
- [独立窗口启动](window-startup.json)：1280×720 图形播放器启动超过 12 秒，扫描未发现运行异常。
- 本轮未改联机契约和规则实现，未重跑双进程 LAN；上一轮的同机联机验证见 [HealthPortals](../HealthPortals/README.md)。真实第二台 Windows 电脑试玩仍待验收。

## 交付

- `Builds/Driftline-MinimalUI-Windows.zip`，63,449,505 字节，302 entries。
- SHA256：`FA391B081C47913B3E05FEADDDD12EB6A5D5DCBD73466885AE2A68DC072DB73E`。
- [打包核对](package.json)：Core / Unity DLL 与验证构建一致，包含启动和 LAN 说明，未包含 DoNotShip 文件夹。
- 旧 `Driftline-HealthPortals-Windows.zip` 哈希仍为 `0C55601F51395BF05C5519EBF1EA4B35B591FB68E046458178D9E4D6630EE6BF`，保留原包。
- 编辑入口见 [UI_EDITING_GUIDE.md](../../UI_EDITING_GUIDE.md)。本轮未提交、未推送 Git；已有其他修改保持原样。
