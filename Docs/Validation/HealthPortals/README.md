# 生命与传送门版本验证

验证日期：2026-10-15。Unity 2022.3.62f3，Windows 本地 Editor / MCP。规则修订见 [HEALTH_PORTALS_RULES.md](../../HEALTH_PORTALS_RULES.md)。

## 自动验证

- [EditMode](editmode.json)：67/67 通过。包括所有八个传送门端点、出口堵塞、固定中央侧落点、共享气、远端封口围杀、传送途中急停拒绝、扣血、备用池重用、八子上限、生命归零、同时出局平局、快照序列化。
- [PlayMode](playmode.json)：16/16 通过。包括真实合成鼠标点击菜单、棋子、移动终点、急停、部署、骰子，五种主题与环境交互，以及堵塞传送门解封后的最终模型位置、滚轮缩放、UI 阻止缩放和生命 HUD。
- 测试使用官方 InputTestFixture 隔离输入，并在场景加载前建立隔离。套件断言恢复原始设备集合、启用状态及当前鼠标。[执行后设备记录](input-devices-after-tests.json)确认原生鼠标与键盘启用，没有残留合成鼠标。
- [Windows 双进程 LAN](lan-summary.json)：25 个共同 revision（0–24）摘要一致，包含客户端断线重连；星舰机库、状态 1、环境种子 74019 同步。报告包含生命与场上数量字段并验证范围；这次自动联机过程没有发生扣血，扣血后快照往返由 EditMode 验证。
- [独立图形窗口启动](window-startup.json)：1280×720 存活超过 12 秒，扫描未见游戏运行异常。

真实两台 Windows 电脑间的防火墙、实际网络连接和联机完整对局仍待用户试玩，本轮双进程验证不替代该验收。

## 画面检查

- [主菜单，1366×768](../../Review/HealthPortals/menu-1366.png)：只提供五种设计主题，无环境状态选项。
- [对局，1366×768](../../Review/HealthPortals/game-1366.png)：默认距离 26 的近景、完整棋盘、生命与场上棋子数量。
- 编辑器刚切换分辨率时截图曾出现过渡帧裁切；稳定后的实际尺寸与布局已重新截图检查。

## 构建与包

- [构建记录](build.json)：成功，0 errors，3 warnings；一个 URP TerrainDetail D3D11 shader 警告及两个 Feel 自带后处理资源缺失脚本警告。详见 [Editor 消息](editor-build-messages.json)。
- Editor 在手动退出 Play Mode 时仍记录 Unity Test Framework 1.1.33 的 BackgroundWatcher 空引用，来源为测试包 PlaymodeLauncher.cs:113；该问题未阻止测试通过或独立播放器运行，未改动第三方包。
- ZIP：`Builds/Driftline-HealthPortals-Windows.zip`，63,453,689 字节。
- SHA256：`0C55601F51395BF05C5519EBF1EA4B35B591FB68E046458178D9E4D6630EE6BF`。
- [打包核对](package.json)：302 entries，Core / Unity DLL 与实际验证的构建一致，包含启动与 LAN 说明，排除 DoNotShip 调试文件夹。
- [历史包校验](previous-packages.json)：四个历史 ZIP 保留原哈希。
- 联机契约为 `board19-health5-pieces8-portals-5`，双方应使用新包。
- 本轮未提交、未推送 Git。项目已有截图删除及其他历史修改保持原样。Editor 已退出 Play Mode，测试临时改变的项目设置已恢复。
