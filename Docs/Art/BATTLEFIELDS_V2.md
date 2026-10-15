# 精制棋盘场景 V2

本轮落实已确认的重搭方向：保持 19×19 地图、规则、移动路径与九处机关不变，重做相连墙体、棋盘地台和外围场景。使用项目已有 Polyperfect、KayKit、Polyworks 模型，并在项目自有目录保存 URP 材质适配和可编辑 Prefab。

## 场景及互动

| 主题 | 场景构成 | 四角直接互动 | 环境状态 |
| --- | --- | --- | --- |
| 前线要塞 | 带厚度的演习庭院、连续沙袋防线和回折端头、指挥部与碉堡、补给道路、车库与前哨检查站 | 信号天线扫描、补给箱开合、朝场外的炮架后坐、维修吉普启动 | 晴日驻防 / 黄昏战损 |
| 符文地城 | 封印石庭、连续石廊、廊柱与入口、中央祭坛、献礼台和炼金工作区 | 封印冠转动、药剂晃动与冒泡、遗物箱开合、廊道拉杆 | 封印苏醒 / 湿冷遗迹 |
| 星舰机库 | 金属训练甲板、连续舱壁与肋架、相接风管、停机棚、带危险标线的检修槽与装卸传送带 | 雷达扫描、载货平台升降、维修臂转动、冷却阀门 | 正常运转 / 检修警戒 |
| 暮林祭坛 | 古代石庭、混合树种的后方密林、岩石岸坡、树根与林下草丛、营地和石阶 | 守护符石、泉眼涟漪、营火添柴、花丛萤火 | 林间日光 / 雨后薄雾 |
| 潮汐港口 | 有水面和厚度的石码头、连接木栈道和扶栏、横向停泊商船、仓屋、成组堆叠货物 | 船钟摇动、吊机货物升降、收竿水花、航标开启 | 白昼装卸 / 暮色潮雾 |

每套四个互动都被发现后，后方信标触发一次本地发现效果。没有道具、数值或规则奖励。每套至少两个开关式互动可再次点击还原。

## 在 Unity 中修改

- `Assets/Themes/BattlefieldsV2/<theme>/Stage.prefab`：进入 Prefab Mode 调整完整场景。旧首版留在 `Assets/Themes/Battlefields`。
- `Architecture — static/BoardSurface`：棋盘、格线和阵营/传送标记。
- `Architecture — static/ConnectedWalls`：整体轮廓网格和主题接头。占用格共享精确边界，不以一段直模型替代转角。外露侧面生成、相邻内部面省略；厚墙内部有完整顶面。
- `Architecture — static/SurroundingSet`：道路、地台、边墙、树木、船坞和工作区。
- `MovingWalls`：九个 `ThemeMovingWall` 只跟随现有规则墙的显隐和动画；不要移动根部的格子坐标。
- `FourCornerInteractions`：每个小场景保留可见部件、实际转轴、触发框、声音、局部粒子和反馈灯。`ThemeSceneryInteraction.Motions` 可配置多个部件的位移、旋转和缩放；`Toggle` 控制是否保持第二状态。
- `Environment — standard` / `Environment — alternate`：两个状态的布景。`ThemeEnvironment` 设置灯光和背景；`SeededDetails` 只改变外围细节显隐。
- `Assets/Resources/Themes/*Theme.asset`：主题名称、两种环境名称、镜头构图和 Prefab 引用。
- `Assets/UI/Prefabs/UIRoot.prefab`：菜单中的 `EnvironmentSelector`、`EnvironmentLabel` 和底部 `SceneryHint` 均为保存的 UI，可在编辑器中直接修改。

美术转轴和粒子不加入静态合批。固定建筑和棋盘使用项目自有的可读网格副本进行一次静态合批。第三方源资产保持不变。

`Driftline/Battlefields V2/Author All Missing Stages` 仅补充缺失场景，不覆盖已有 Prefab 的手工修改。代码中的 `Author(id, true)` 是显式重建入口，会覆盖该 V2 Prefab 的美术调整，仅用于需要从制作脚本重新生成时。正常运行不执行场景生成。

## 输入与联机约定

环境热点使用 `Ignore Raycast` 层的 Trigger，只由环境专用查询命中；没有阻挡棋子的物理碰撞。热点位于棋盘核心之外，点击投影落在核心时也不会消费规则点击。UI、设置和规则引导优先。选棋后的环境点击保留当前预览；等待远端玩家时可继续互动。

主菜单先选主题，再选环境状态或随机环境。进入对局后两项锁定。房主广播主题版本、实际环境状态和布景种子；客户端及重连客户端跟随房主。本机的首选项不被远端覆盖，返回菜单后恢复。外围点击和发现效果属于各自的本地表现，不广播，不写入 `MatchSnapshot`。

简化特效选项关闭循环天气/氛围粒子和点击粒子，保留能看懂交互的部件动作与提示。总音量控制所有互动音效。

## 复查入口

- EditMode：主题目录、十种连接掩码、热点边界、材质及网络字段往返。
- PlayMode：五套主题中的实际鼠标选棋/移动、20 个环境热点、保留选择、远端回合、菜单预览、引导遮挡、环境重现和对局锁定。
- `BattlefieldReviewCapture.CaptureStills()`：Play Mode 中从真实 Game View 导出五套主题的标准/变化状态，以及主菜单。
- `BattlefieldReviewCapture.CaptureMotion()`：导出五套主题的环境互动帧，可编码为视频；它是演出预览，实际鼠标验收由 PlayMode 测试负责。
- `DraftlineBuild.BuildWindowsBattlefields()`：独立构建到 `Builds/BattlefieldsV2`，保留之前的正式版和首版主题构建。

最终测试、截图与同机双进程报告见 `Docs/Validation/BattlefieldsV2`。同机验证不能代替第二台 Windows 电脑的真实局域网试玩。
