# 美术制作前正式基线

2026-10-15，Git 标记 `pre-art-final-20261015`。这是五个场景美术主题制作之前的功能基线。

包含可编辑 Canvas UI、本地玩家/电脑自由组合、棋盘直接点按与急停、行动原因预览、可关闭规则引导、局域网权威状态与重连。主题仍为界钥棋庭桌面版。

上一轮验收：EditMode 36/36、PlayMode 8/8；Windows Development 构建成功，0 error / 0 warning；同机两进程局域网状态一致并完成重连。真实第二台电脑的局域网试玩尚未验收。详细原始记录见 `Docs/Validation/2026-10-14/`。

该基线归档包为 `Builds/Driftline-Windows-LAN.zip`，SHA256：`E006224DB92A3DC607439161D4DE15983CEB0498FC7DA1D3D200785F67FBBE26`。Builds 被 Git 忽略，二进制包另行保存在本机；后续美术构建使用不同名称。

美术制作范围与资产来源见 `Docs/Art/THEME_BOARD_PLAN.md`。本基线不包含后续主题系统或五张场所棋盘的实现。
