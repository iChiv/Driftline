# Battlefield V2 验证记录

日期：2026-10-15。环境：原生 Windows，Unity 2022.3.62f3，Windows 侧 Unity MCP。全程未使用 WSL。

## 结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| EditMode | 48/48 通过 | [editmode.json](editmode.json) |
| PlayMode | 12/12 通过 | [playmode.json](playmode.json) |
| 实际 Game View | 五主题各两种状态、主菜单，另有五张角落近景 | [画面与视频](../../Review/BattlefieldsV2/README.md) |
| 1366×768 下拉菜单 | 已检查，三项均可读，弹出列表与控件等宽 | [截图](../../Review/BattlefieldsV2/environment-menu-1366.png) |
| 简化特效 | 开启时循环粒子停止，引擎仍保持开启状态；关闭后排气恢复 | [reduced-effects.json](reduced-effects.json) |
| 编辑器运行日志 | 场景复查期间 0 条错误 | [editor-console.json](editor-console.json) |
| Windows 开发构建 | 成功，180,980,032 bytes，0 errors / 3 warnings | [build.json](build.json) |
| 图形播放器启动 | 1280×720，D3D11，运行 12 秒仍存活，未发现运行异常 | [window-startup.json](window-startup.json) |
| 同机双进程联机 | 25 个共享 revision（0–24）快照摘要相等，客户端断线重连成功 | [lan-summary.json](lan-summary.json)、[房主](host.jsonl)、[客户端](client.jsonl) |
| 场景同步 | 房主机库/检修状态/seed 74019，客户端初选森林/标准/seed 222，连接与重连均采用房主配置 | 同上 |
| 包内容 | 302 项，程序集与已验证构建相同，保留字体许可证和试玩说明 | [package.json](package.json) |
| 历史构建 | 美术前正式版与主题首版 ZIP 的 SHA256 均未改变 | 同上 |

运行测试覆盖棋盘直接选棋、终点移动、急停、出子、重启动画取消、规则引导，以及五套场景的 20 个环境热点、选棋保留、远端回合、菜单预览、环境重现与对局锁定。本轮还增加了直接点击前方骰子开始投掷的回归。最后的固定地基/步道几何整理后再次运行 EditMode，并重新导出全部实景图。

构建的三条警告已保存在 [build-console.json](build-console.json)：一条来自 URP 14.0.12 的 Terrain shader 循环展开；两条来自 Feel 内置 PostProcessing Resources 的缺失脚本。正式 SampleScene 中缺失脚本数量为 0，也未放置这两个旧后处理资源。本轮未修改第三方源资源。

## 试玩包

`Builds/Driftline-BattlefieldsV2-Windows.zip` — 63,446,020 bytes。

SHA256：`44DE7EAD4ED440B44A1BADD73A85CB8EBC639349A4BB966BDFC1B994CFE29849`

程序集 SHA256：`682CB3FFF6D288FF1F1CC99FCAEF349EA8568CE3AE417A80F4B35E7A825262DB`

两台电脑都使用此包进行后续真实局域网试玩。同机 loopback 验证不代表已完成双机网络、系统防火墙和实际延迟验收。

## 复现

1. 在 Unity 中执行 `DraftlineBuild.BuildWindowsBattlefields()`。
2. Windows PowerShell 运行 `./Tools/Validate-BattlefieldsLan.ps1`。
3. 运行 `./Tools/Package-Battlefields.ps1` 生成新的 ZIP；该脚本保留已有同名包，不覆盖已有归档。

包外的场景编辑说明见 [BATTLEFIELDS_V2.md](../../Art/BATTLEFIELDS_V2.md)。所有美术布局、两种环境状态、交互转轴和 UI 控件都保存在项目资源中，正常运行不重建场景。

## 临时文件

自动审批拒绝了对 `Temp/BattlefieldMotion` 原始录像帧及两张旧预览图的删除，返回 `blocked by policy`。因此保留这些本地临时文件；它们未打入试玩包。
