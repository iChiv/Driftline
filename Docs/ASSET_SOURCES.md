# 新增公开资产

## Source Han Sans CN Regular

- 文件：`Assets/UI/Fonts/SourceHanSansCN-Regular.otf`
- 来源：[Adobe Source Han Sans](https://github.com/adobe-fonts/source-han-sans/blob/release/SubsetOTF/CN/SourceHanSansCN-Regular.otf)
- 官方授权：[SIL Open Font License 1.1](https://github.com/adobe-fonts/source-han-sans/blob/release/LICENSE.txt)
- 完整授权与版权声明随字体保存在 `Assets/UI/Fonts/LICENSE.txt`。
- 下载日期：2026-10-14。
- 用途：uGUI 与棋盘文字标记的中文/英文显示，替代不能在 uGUI 中正确显示的旧字体引用。
- 字体未修改。首次生成的 UI Prefab 和目标 Prefab 引用该字体，可在 Inspector 替换。
- Windows 构建旁会复制 `SourceHanSans-LICENSE.txt`，分发时保留。

## 本次复用的项目资产

没有新增购买或下载其他美术包。以下均来自实施前项目中已有文件，原许可仍以随包文件和项目已有授权为准。

| 资产 | 实际使用 |
| --- | --- |
| `Assets/KayKit/Packs/KayKit - Dungeon Remastered Pack (for Unity)/Models` | 四色旗帜、箱桶与火把；桌边叙事摆件使用 URP 适配材质 |
| 现有棋盘、兵棋模板与 Dice Rig | 保留规则坐标和骰子映射，低墙/颜色/印记统一视觉 |
| `Assets/Resources/FX` | 吃子、传送与胜利模板，由 MatchFeedback 配置引用 |
| `Assets/Resources/Sfx` | 事件音效，DraftlineAudio 支持 Inspector 覆盖 |
| `Assets/Feel/FeelDemos/Wheel/Sounds/FeelWheelMusic.wav` | 低音量循环背景音乐，可在场景中替换或移除 |

故事文案、阵营印记、选中环网格、uGUI 布局与交互提示为本次项目内制作。音量层级已配置，音乐的风格匹配和连续对局听感仍需人工试听。
