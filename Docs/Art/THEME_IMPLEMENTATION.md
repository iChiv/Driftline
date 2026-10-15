# 场所棋盘主题首版

2026-10-15。在 `pre-art-final-20261015`（`0bb7646ded070d3a8df35671cd7988126b7c614e`）之后制作。原桌面主题保留；五张场所棋盘均已制作为保存的 Prefab，主菜单提供六个选项并即时预览。

| 主题 | 场内表现 | 周边构成 |
| --- | --- | --- |
| 前线要塞 | 石质庭院、低沙袋、装甲升降块、士兵兵棋 | 后方指挥所/营房/车辆门，左侧补给帐篷和卡车，右侧地堡/吉普，前方沙袋与道路 |
| 符文地城 | 石砖、半墙、封印机关、骑士兵棋 | 后方祭坛/拱门，两侧石柱/廊道与暖色火光，前方台阶，四角补给营地 |
| 星舰机库 | 金属甲板、低货柜隔断、机械挡板、宇航员兵棋 | 后方机库/舱门/观察窗，左侧维修终端/发电机，右侧货运箱/输送带，前方护栏/管线 |
| 暮林祭坛 | 旧石台、低石垣、符石机关、部族探索者兵棋 | 后方德鲁伊祭坛/残拱，两侧低树冠林带，前方苔草与石阶 |
| 潮汐港口 | 石质集散场、木货箱、升降闸板、海盗兵棋 | 后方泊船与栈桥，左侧吊机，右侧仓屋/调度棚与码头地基，前方木桩/桶货，外围水面 |

四方底座保持红蓝绿黄，并加 I–IV 标识；四对传送保持圆/菱/三角/方形符号与配色。每个模型墙下都有占格底座；地面额外标出 19×19 的规则格线。默认镜头为新主题留出外围空间，纯俯视入口继续保留。

## 直接编辑

- 场景入口：`Assets/Scenes/SampleScene.unity` → `ThemeContainer` → 五个主题实例。
- 正式场景 Prefab：`Assets/Themes/Battlefields/{fortress,dungeon,hangar,forest,harbor}/{id}.prefab`。
- 模型兵棋：各主题目录 `Pawn.prefab`。修改 `Miniature`，保留 Faction base、碰撞代理与 `ThemePieceAppearance` 引用。
- 配置：`Assets/Resources/Themes/{id}Theme.asset`，可编辑名称、背景故事、环境色与镜头距离。`ThemeId` / `Version` 是联机契约，已发布后不要只在一端改动。
- 主菜单：`Assets/UI/Prefabs/UIRoot.prefab` → `MainMenuPanel/MenuCard/BoardThemeSelector`。布局、文字颜色、下拉模板均保存，可直接手改；运行时只绑定选项、主题故事和状态。
- 编辑器预览：选中 `ThemeContainer` 的 `BoardThemeScene`，设置 `PreviewThemeId`，组件菜单执行 `Preview Selected Theme`。

各主题包含 `BoardSurface / FixedWalls / MovingWalls / FactionGates / PairedPortals / NearSet / Landmarks / Lighting / ReadabilityAndSetDress`。摆放、缩放、材质和灯光都是正式场景数据；运行时切换已有实例，不重建美术布局。

`Driftline/Themes` 下的制作菜单是一次性作者工具：已有主主题文件不会被 Produce 覆盖。**日常美术修改直接编辑 Prefab，不重新执行生成/Polish 菜单。**如添加模型后需要合批，执行 `Prepare Static Geometry Batching`；只调整静止几何的运行时合批引用，不用于重置场景构图。

## 工程处理

只使用项目已有的 Polyperfect、Polyworks、KayKit 与字体资源，没有新增外部模型下载。适配的 Prefab Variant / URP 材质位于 `Adapters` / `Materials`。保留模型纹理、UV、多材质、Specular/法线/遮罩等已有引用；未批量改写第三方包。棋盘地砖使用独立的低对比主题铺色，让规则物件更清楚。

`Meshes` 保存静止模型的可读几何副本，供 `ThemeStaticGeometry` 首次激活时合批；原始导入器保持不变。静止环境合批，不合并升降墙、兵棋、动态文字或交互标记。合批后这些几何在运行时不应再单独移动；在 Edit Mode 改完 Prefab 后重新 Play 即可查看新摆放。参见 [Unity 2022.3 StaticBatchingUtility.Combine](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/StaticBatchingUtility.Combine.html)。

规则仍由原 `BoardLayout` 和 `MatchRules` 决定。美术模型不带碰撞体；沿用原有规则碰撞代理。`ThemeMovingWall` 以显式列/行坐标绑定原升降动画驱动，所有装饰子件一起显示/隐藏。`ThemePieceAppearance` 只把阵营色应用到底座，人物原有材质保持。

本地主题保存到原 `boardTheme` 偏好；对局开始后不能切换。LAN 房主主题 ID/版本进入房间状态，客户端采用房主主题但不覆盖自己的本地偏好；退出菜单恢复本地选择。协议 `board19-v03-themes-3`，两台电脑须使用本轮同一游戏包。

## 首版范围

本次完成场所构成、模型兵棋、完整规则物件、静态水面/水纹、灯光与菜单切换。复用已验证的事件特效、音乐和音效；尚未为每个场所录制独立声景或制作复杂水面 Shader、角色骨骼动作、天气系统。这些可以基于本版的实际试玩反馈继续细化，不影响主题切换与现有规则。

测试、构建和双进程网络证据见 `Docs/Validation/2026-10-15/README.md`。`Docs/Review/2026-10-15-{id}-menu.png` / `-play.png` 为本轮准备对局截图，不能替代人工完整试玩。
