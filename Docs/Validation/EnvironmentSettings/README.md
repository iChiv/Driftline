# 环境与设置版本验证

2026-10-15，Unity 2022.3.62f3，Windows 本地 Editor / MCP。

## 已实现

- 五套主题共用 ThemeGradient shader，各自保存下层、上层与远景雾色。ThemeBackdrop 默认 1 秒过渡，支持中途再次切换；UseGradient 关闭时回退纯色。环境状态改变色调，背景不参与规则或随机数。
- 每主题新增 16 个贴地细节、8 个固定墙顶模型、12 个外缘模型，全部保存在主题 Prefab 的 InfillDressing，可在 Inspector 修改。
- 外侧空地不是不可达区：穿墙状态下，从 (7,5) 向上可经过 (7,4)、(7,3)、(7,2)、(7,1) 到 (7,0)。因此没有将红圈空地封为大型环境区。较大模型移到墙顶与棋盘外缘，贴地细节避开棋格中心半径 0.35，不带 Collider，不拦截点按。
- 局域网加入地址默认为空；说明可展开。房主显示真实网卡 IPv4 与端口并可复制，多网卡可选择；IP:端口 可直接粘贴加入框，自动拆分。回环地址仅用于同机；监听所有网卡的原传输设置保留。
- 显示设置提供 720p / 1080p / 1440p / 4K 和窗口 / 无边框全屏。应用后 12 秒内保留才写入本机偏好，超时、恢复或关闭设置会还原；后续启动读取保留设置。Editor 不改变系统窗口，实际切换在独立播放器验证。
- 中英文切换按计划留到后续。

## 证据

- [EditMode](editmode.json)：84/84 通过，覆盖既有规则、五主题布景边界、无碰撞体、穿墙外侧通路、网卡过滤和复制端点解析。
- [PlayMode](playmode.json)：21/21 通过，包括背景过渡不改变对局、显示设置确认与超时恢复、局域网说明与地址校验，以及原有棋盘、主题、骰子、急停、传送门、菜单和镜头操作。
- 加入地址解析完善后，[局域网 UI 定向回归](lan-ui-final.json) 1/1 再次通过。
- [独立 Windows 显示验证](display-summary.json)：19 条记录全部通过。四个分辨率在两种模式下的实际尺寸与请求一致；确认保存、超时恢复与关闭设置恢复通过。探针完成后恢复原偏好。
- [双独立进程 LAN](lan-summary.json)：25 个共同 revision（0–24）摘要一致，重连成功；机库、环境 1、种子 74019 一致，两个进程退出码 0。真实第二台电脑的网络与完整联机对局仍待验收。
- [输入恢复](input-after-tests.json)：仅原生键盘和鼠标，均启用，当前鼠标为原生；Editor 已退出 Play Mode，测试临时修改的项目设置已恢复。
- [画面](../../Review/EnvironmentSettings/README.md)：五主题背景、布景俯视、设置、确认、连接说明和房主网卡。
- [构建](build.json)：成功，0 errors，3 warnings。构建警告摘录见 [build-warnings.txt](build-warnings.txt)。
- [Editor 消息](editor-build-messages.json)仍记录手动退出 Play 时 Unity Test Framework 1.1.33 BackgroundWatcher 的既有空引用（PlaymodeLauncher.cs:113）；独立显示与 LAN 播放器日志未检出游戏运行异常，未修改第三方测试包。

## 交付

- `Builds/Driftline-EnvironmentSettings-Windows.zip`，63,469,752 字节，302 entries。
- SHA256：`1A328D21F8255D107B0564BF8C123027A0CD1BE5CAA02BB9760A4E642AB829E4`。
- [包核对](package.json)：Core / Unity DLL 与实际验证的构建一致，包含启动和 LAN 说明，无 DoNotShip 文件夹。Core DLL 与上一版 MinimalUI 相同，规则程序集未改变。
- 历史 MinimalUI、HealthPortals ZIP 保持原哈希。本轮未提交、未推送 Git；已有其他工作区修改保留。
- 编辑入口见 [UI_EDITING_GUIDE.md](../../UI_EDITING_GUIDE.md)。新增布局和场景均为保存的资产，不在运行时重新搭建 UI 或布景。
