# 棋盘主题入口（2026-10-15）

主菜单新增 `MainMenuPanel/MenuCard/BoardThemeSelector`，六个已实现选项由 `Resources/Themes` 的 BoardTheme 配置提供。下拉背景、字号、行高、模板和位置仍在 UIRoot Prefab 中编辑；主题名称/故事改对应 Theme asset。选择后立即预览并保存本地偏好，对局中锁定；LAN 客户端跟随房主，返回菜单恢复本地主题。

五个场景模型的编辑入口与边界见 [主题制作记录](Art/THEME_IMPLEMENTATION.md)。

# Driftline UI 编辑入口

当前正式 UI 使用 uGUI，位于 `Assets/Scenes/SampleScene.unity` 中的 **UIRoot**，源 Prefab 是 `Assets/UI/Prefabs/UIRoot.prefab`。运行时只绑定数据和事件，不重新搭建页面。原 UXML/USS 保留作迁移参考，正式场景不再挂 UIDocument。

## 页面

| Hierarchy 路径（UIRoot 下） | 用途 |
| --- | --- |
| MainMenuPanel/MenuCard | 开局故事、本地游戏、LAN、设置与退出 |
| MatchSetupPanel/Configuration | 四个席位、电脑难度与开局 |
| LanLobbyPanel/Room | IP/端口、人数、颜色、准备、开局与房间状态 |
| MatchHUD/PlayerPanel | 当前回合、库存、四方状态、骰子与设置 |
| MatchHUD/Objective、EventLog | 胜利目标与对局记录 |
| MatchHUD/ActionExplanation | 悬停目标的行动预览、最近行动与吃子原因 |
| RuleGuidePanel/GuideCard | 七段可选规则引导、符号示例与翻页 |
| PauseAndSettingsPanel/SettingsCard | 音量、简化效果、返回与结束对局 |
| ResultPanel/Victory | 胜利原因、再开局与返回 |
| StatusMessage | 操作失败提示；没有错误时隐藏 |

选中 UIRoot 的 DraftlineCanvasUI 组件，在 Inspector 的页面预览按钮中切换页面，不需要进入 Play Mode。也可直接启用一个页面并关闭其他页面。运行时根据游戏状态切换可见性，不覆盖布局；编辑完将预览切回主菜单再保存。

## 修改方式

- 双击 `UIRoot.prefab` 在 Prefab Mode 编辑；或直接修改场景实例并保留 Override。修改全项目通用布局时 Apply 回 Prefab，场景专属调整保留 Override。
- 使用 RectTransform 改位置、宽高和锚点；Image 改背景/图标/颜色；Text 改静态文案、字体、字号、颜色。
- 所有 Text 当前使用 `Assets/UI/Fonts/SourceHanSansCN-Regular.otf`，授权文件同目录保存。它是实际字体资源，可供独立构建使用。
- `DraftlineCanvasUI` 组件的字段指向保存的控件。替换控件或删除节点时同步维护引用；只调整外观不需要改代码。
- Turn、Dice、Hint、Player、Entries、Result、Error 等动态文本在运行时更新内容。它们的尺寸、字体和外观仍由组件控制；静态故事/标题/按钮文案来自保存的控件。
- `Assets/UI/Config/MatchTextContent.asset` 保存动态阵营名称、四方背景、道具名称/说明和两种结局文案。顺序分别对应红/蓝/绿/黄与穿墙/急停/撞吃/破墙；可改内容，不改变数组顺序。游戏控制器的 Content 字段引用它。
- `Assets/UI/Config/RuleGuideContent.asset` 保存引导的 Title / Body / Example。UIRoot 的 GuideContent 引用它，布局、字体与按钮在 RuleGuidePanel 编辑。可从主菜单、配置、HUD、设置打开；LAN 对手不因本机查看引导而暂停。
- Dropdown 的 Options 在 Inspector 编辑。席位选项顺序固定为“关闭 / 玩家 / 电脑”，难度固定“简单 / 普通”；改变业务选项需要同步枚举映射。
- 下拉展开项位于 `Dropdown/Template/Viewport/Content/Item`，行高 44，Item Label 留有 36 的文字高度。原 17 高的文字框放不下思源黑体 19 号字所需的 27 高，导致展开项看起来为空白；修改字体/字号时同步检查展开模板。根控件和 Item Toggle 的 Colors 控制背景/悬停/选中色，Text 控制文字色。当前为深色背景、浅色文字、金色选中标记。
- CanvasScaler 以 1920×1080 为参考，并随窗口缩放。检查 1366×768、1280×720 后保存调整。

## 棋盘提示与会话

- `Assets/UI/Config/BoardInteractionStyle.asset`：终点尺寸/高度、路径宽度/高度、正常/急停颜色、模板和材质。
- `Assets/Prefabs/BoardInteraction/BoardTarget.prefab`：终点、方向箭头、急停与出子的共用视觉；Symbol 是动态标记，外观可编辑。
- `Assets/Prefabs/BoardInteraction/SelectionRing.prefab`：选中圈，Selection.mat 修改其颜色。
- `Assets/Prefabs/BoardInteraction/BoardContextActions.prefab`：棋子上方的道具按钮及悬停说明，布局、字号、背景均可编辑。BoardInteractionStyle 的 ContextOffset 调整它相对棋子的位置，Selected/Hover 调整选中/悬停色。
- 场景 `Draftline Game` 上的 MatchSession：四个颜色席位和控制方式。本地入口首次默认一位玩家对一位电脑，其余两方关闭；配置页可自由组合 2–4 方，至少一位玩家。返回菜单或进入 LAN 页面再返回会保留本次运行中的本地配置。
- 场景 `NetworkRoot`：保存的 NetworkManager 和 UnityTransport，IP/端口在房间页配置。

`Driftline/Install Editable Experience` 仅用于第一次迁移。检测到正式 Canvas 后会保留已有编辑；**不要为了调整 UI 重新生成页面**。

`Driftline/Add Action Explanation And Rule Guide` 为首次增补工具，检测到引导面板就保留已有编辑。行动说明来自规则事件；红圈颜色由 BoardInteractionStyle.Danger 控制，沿用 SelectionRing 模板。预览不显示尚未生成的道具种类/位置。

目前没有将需要坐标计算的棋子/停点按钮作为正式流程。点棋子预览、点绿色终点滑行、点金色停点急停；右键/Esc 取消预览，已提交动作不会撤销。

## 场景、特效与声音

- 场景 `Tabletop Identity`：旗帜、阵营印记、传送配对符号与桌边叙事物件，可直接移动、替换或禁用；装饰没有用于阻挡棋子的 Collider。
- `Assets/Themes/Driftline/Materials`：石墙、机关墙、木桌与摆件材质；当前 Theme 为 tabletop，运行时主题应用会按该资源的材料引用赋值。
- `Assets/Themes/Driftline/MatchFeedback.asset`：吃子、传送与胜利的粒子 Prefab、缩放、存活时间和同时存在数量上限。
- `Draftline Game/DraftlineAudio`：Overrides 可覆盖各事件音效；BackgroundMusic 指向子节点 `Background Music` 上保存的 AudioSource，可换 Clip、调低音量或禁用。玩家设置中的音量控制全局 AudioListener；简化效果关闭大型粒子和相机震动。
- 本次新增的 Install Editable Experience、Add Editable Context Actions、Author Tabletop Identity 菜单采用已有节点保护。正式修改通过 Inspector、Prefab 和场景保存，不需要再执行生成菜单。旧 Bake Tabletop / Dungeon Skin 菜单保留为历史工具，会改写主题材质或场景外观，不用于调整当前 UI。
