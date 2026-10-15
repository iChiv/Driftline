# 棋盘主题入口（2026-10-15）

中英文入口：主菜单 `LanguageSelector`，UIRoot 的 LocalizationPresenter 绑定它。文案表为 `Assets/Resources/Localization/ChineseEnglish.txt`，每行 `中文|English`，换行写 `\n`，动态数值写 `{0}` 等占位符。语言偏好保存为 `Driftline.Language`（0 中文，1 English）。UI 数据仍使用原文与枚举，翻译只改变显示，不同步或改写棋局状态。新增动态文字应补入文案表，并检查切换回中文以及英文布局。

升降墙修复：每套场景 `MovingWalls/Lift socket c,r/Animated body` 中的子模型现在使用机关槽局部坐标。ThemeMovingWall 的 GeometryVersion 为 1；不要将子模型改回世界原点。Body 由既有规则驱动升降，其正常位置／缩放来自控制器缓存，不读取动画中途的临时姿态。显式迁移工具 `Driftline/Repair Lift Wall Geometry` 只修复旧版本，日常模型调整直接编辑 Prefab。

本轮环境与设置：主题资源新增 BackgroundMaterial / BackgroundTop / BackgroundHaze，场景 BoardThemeScene 同节点的 ThemeBackdrop 控制渐变与 1 秒过渡，可关闭 UseGradient 回退纯色。环境标准／变化状态会调整背景色调；不依赖运行时生成贴图。

`PauseAndSettingsPanel/SettingsCard` 新增 DisplayResolution、DisplayMode、ApplyDisplay、DisplayConfirmation。根节点的 DisplaySettingsPanel 保存控件引用与确认时限（默认 12 秒）；Windows 独立运行时实际改变显示模式，Editor 只验证选择与确认流程。保留后写入本机 PlayerPrefs，超时、关闭设置或取消时恢复。

`LanLobbyPanel/Room` 的 ConnectionHelpButton 打开精简连接说明，HostAddressPanel 显示网卡地址与可复制端点。加入地址默认空，可输入 IPv4 或直接粘贴 IP:端口。后者自动拆分填回地址与端口字段。主机仍监听所有网卡，地址下拉只用于分享，不改变监听范围。

各主题 Prefab 新增 InfillDressing，ThemeInfillDressing 字段列出贴地细节、固定墙顶物件和外缘模型。红圈区域可用穿墙到达，因此不放置封路的大型物件；贴地细节不进入棋格中心半径 0.35 的区域，装饰均无 Collider。详见 [本轮记录](Validation/EnvironmentSettings/README.md)。

主菜单 `MainMenuPanel/MenuCard/BoardThemeSelector` 只显示五套已设计的场景，旧 tabletop 不再出现在目录中。下拉背景、字号、行高、模板和位置仍在 UIRoot Prefab 中编辑；主题名称改对应 Theme asset。选择后立即预览并保存本地偏好，对局中锁定；LAN 客户端跟随房主，返回菜单恢复本地主题。主菜单只保留标题与功能入口，故事和常驻操作提示已隐藏。

环境自动随机，原 EnvironmentSelector / EnvironmentLabel 在保存的 Prefab 中隐藏，不再由玩家选择。每方生命 5、场上上限 8，HUD 的席位卡显示两项数值。当前传送门与生命规则见 [规则修订](HEALTH_PORTALS_RULES.md)。

场景 `Draftline Game / DraftlineCameraDirector` 保存对局距离 26、缩放范围 20–36、俯角与目标点。MenuShots 保存三组菜单观察机位，MenuHoldSeconds / MenuBlendSeconds 控制停留和过渡，MenuDriftDegrees 控制轻微漂移，MenuBoardOffset 留出左侧菜单空间。菜单切换机位；配置、设置、游戏说明和悬停场景机关时暂停轮巡；开始对局恢复稳定视角。滚轮在棋盘上缩放，在 UI 上交给 UI 滚动。

圆角 Image 使用 `Assets/UI/Config/RoundedSurface.asset` 的九宫格 Sprite，圆角保持固定尺寸；修改尺寸仍使用 RectTransform。`Assets/Editor/DraftlineMinimalUI.cs` 是显式迁移工具，不在游戏中生成 UI；日常调整直接编辑 Prefab。重新运行迁移会覆盖对应布局，已有手工调整时不要重跑。

五个场景模型的编辑入口与边界见 [主题制作记录](Art/THEME_IMPLEMENTATION.md)。

# Driftline UI 编辑入口

当前正式 UI 使用 uGUI，位于 `Assets/Scenes/SampleScene.unity` 中的 **UIRoot**，源 Prefab 是 `Assets/UI/Prefabs/UIRoot.prefab`。运行时只绑定数据和事件，不重新搭建页面。原 UXML/USS 保留作迁移参考，正式场景不再挂 UIDocument。

## 页面

| Hierarchy 路径（UIRoot 下） | 用途 |
| --- | --- |
| MainMenuPanel/MenuCard | 标题、主题、本地游戏、局域网、游戏说明、设置与退出 |
| MatchSetupPanel/Configuration | 四个席位、电脑难度与开局 |
| LanLobbyPanel/Room | IP/端口、人数、颜色、准备、开局与房间状态 |
| MatchHUD/PlayerPanel/TurnCard | 当前方、阶段、剩余滑行、掷骰与游戏说明 |
| MatchHUD/PlayerPanel/InventoryCard | 有道具时显示的库存与说明 |
| MatchHUD/PlayerPanel/PlayerRoster | 按已启用席位排列的阵营状态卡 |
| MatchHUD/Objective、EventLog | 历史节点，已隐藏 |
| MatchHUD/ActionExplanation | 悬停预览；吃子或无路可走后的说明显示 5 秒 |
| RuleGuidePanel/GuideCard | 游戏说明：胜利条件首页，后续四页按需查看操作与机制 |
| PauseAndSettingsPanel/SettingsCard | 音量、简化效果、返回与结束对局 |
| ResultPanel/Victory | 胜利原因、再开局与返回 |
| StatusMessage | 操作失败提示；没有错误时隐藏 |

选中 UIRoot 的 DraftlineCanvasUI 组件，在 Inspector 的页面预览按钮中切换页面，不需要进入 Play Mode。也可直接启用一个页面并关闭其他页面。运行时根据游戏状态切换可见性，不覆盖布局；编辑完将预览切回主菜单再保存。

## 修改方式

- 双击 `UIRoot.prefab` 在 Prefab Mode 编辑；或直接修改场景实例并保留 Override。修改全项目通用布局时 Apply 回 Prefab，场景专属调整保留 Override。
- 使用 RectTransform 改位置、宽高和锚点；Image 改背景/图标/颜色；Text 改静态文案、字体、字号、颜色。
- 所有 Text 当前使用 `Assets/UI/Fonts/SourceHanSansCN-Regular.otf`，授权文件同目录保存。它是实际字体资源，可供独立构建使用。
- HUD 左侧根节点保持透明且不拦截点击，只有可见卡片接收 UI 点击。玩家状态使用保存的 VerticalLayoutGroup / ContentSizeFitter；关闭的席位不占空位。Camera / Settings 位于左下角。
- `ActionExplanation/ExplanationScroll/Viewport/Details` 是行动说明正文，长内容可用滚轮查看。正文变化时返回顶部；卡片高度随正文调整，UIRoot 的 ActionBodyHeightRange / ActionBottomPadding 控制高度范围与底部留白。没有预览或重要行动信息时隐藏；普通滑行不留常驻说明，事件日志不再显示。
- `DraftlineCanvasUI` 组件的字段指向保存的控件。替换控件或删除节点时同步维护引用；只调整外观不需要改代码。
- Turn、Dice、Player、Result、Error 等动态文本在运行时更新内容。它们的尺寸、字体和外观仍由组件控制；按钮和标题文案来自保存的控件。Hint、Entries、SceneryHint 与故事节点保留引用并隐藏，不再更新操作文案。
- `Assets/UI/Config/MatchTextContent.asset` 保存动态阵营名称、四方背景、道具名称/说明和两种结局文案。顺序分别对应红/蓝/绿/黄与穿墙/急停/撞吃/破墙；可改内容，不改变数组顺序。游戏控制器的 Content 字段引用它。
- `Assets/UI/Config/RuleGuideContent.asset` 保存引导的 Title / Body / Example。UIRoot 的 GuideContent 引用它，布局、字体与按钮在 RuleGuidePanel 编辑。可从主菜单、配置、HUD、设置打开；LAN 对手不因本机查看引导而暂停。
- Dropdown 的 Options 在 Inspector 编辑。席位选项顺序固定为“关闭 / 玩家 / 电脑”，难度固定“简单 / 普通”；改变业务选项需要同步枚举映射。
- 下拉展开项位于 `Dropdown/Template/Viewport/Content/Item`，行高 44，Item Label 留有 36 的文字高度。原 17 高的文字框放不下思源黑体 19 号字所需的 27 高，导致展开项看起来为空白；修改字体/字号时同步检查展开模板。根控件和 Item Toggle 的 Colors 控制背景/悬停/选中色，Text 控制文字色。当前为深色背景、浅色文字、金色选中标记。
- CanvasScaler 以 1920×1080 为参考，并随窗口缩放。检查 1366×768、1280×720 后保存调整。

## 棋盘提示与会话

- `Assets/UI/Config/BoardInteractionStyle.asset`：终点尺寸/高度、路径宽度/高度、正常/急停颜色、模板和材质。
- `Assets/Prefabs/BoardInteraction/BoardTarget.prefab`：终点、方向箭头、急停与出子的共用视觉；Symbol 是动态标记，外观可编辑。
- 普通终点使用圆形方向标记，急停点使用 `StopMarker.asset` 的切角方形与实心方块符号；两者都有 `Contrast Rim` 深色描边，悬停时放大并提亮。`BoardInteractionStyle.StopTargetMesh` 可替换急停形状。更改视觉不会改变目标格、规则或点按半径。
- `Assets/Prefabs/BoardInteraction/SelectionRing.prefab`：选中圈，Selection.mat 修改其颜色。
- `Assets/Prefabs/BoardInteraction/BoardContextActions.prefab`：棋子上方的道具按钮及悬停说明，布局、字号、背景均可编辑。BoardInteractionStyle 的 ContextOffset 调整它相对棋子的位置，Selected/Hover 调整选中/悬停色。
- 场景 `Draftline Game` 上的 MatchSession：四个颜色席位和控制方式。本地入口首次默认一位玩家对一位电脑，其余两方关闭；配置页可自由组合 2–4 方，至少一位玩家。返回菜单或进入 LAN 页面再返回会保留本次运行中的本地配置。
- 场景 `NetworkRoot`：保存的 NetworkManager 和 UnityTransport，IP/端口在房间页配置。

`Driftline/Install Editable Experience` 仅用于第一次迁移。检测到正式 Canvas 后会保留已有编辑；**不要为了调整 UI 重新生成页面**。

`Driftline/Add Action Explanation And Rule Guide` 为首次增补工具，检测到引导面板就保留已有编辑。行动说明来自规则事件；红圈颜色由 BoardInteractionStyle.Danger 控制，沿用 SelectionRing 模板。预览不显示尚未生成的道具种类/位置。

目前没有将需要坐标计算的棋子/停点按钮作为正式流程。点棋子预览、点绿色终点滑行、点金色停点急停；右键/Esc 取消预览，已提交动作不会撤销。

## 场景、特效与声音

- 五套正式场景通过 BoardThemeScene 的 Entries 切换，详见 [场景编辑说明](Art/BATTLEFIELDS_V2.md)。本轮隐藏了骰台说明和装饰文字，保留门的配对符号与出生点标记。
- `Tabletop Identity` 与 `Assets/Themes/Driftline/Materials` 为历史桌面资源；当前正式目录只使用五套设计主题。
- `Assets/Themes/Driftline/MatchFeedback.asset`：吃子、传送与胜利的粒子 Prefab、缩放、存活时间和同时存在数量上限。
- `Draftline Game/DraftlineAudio`：Overrides 可覆盖各事件音效；BackgroundMusic 指向子节点 `Background Music` 上保存的 AudioSource，可换 Clip、调低音量或禁用。玩家设置中的音量控制全局 AudioListener；简化效果关闭大型粒子和相机震动。
- 本次新增的 Install Editable Experience、Add Editable Context Actions、Author Tabletop Identity 菜单采用已有节点保护。正式修改通过 Inspector、Prefab 和场景保存，不需要再执行生成菜单。旧 Bake Tabletop / Dungeon Skin 菜单保留为历史工具，会改写主题材质或场景外观，不用于调整当前 UI。
