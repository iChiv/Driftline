# 工程验证记录

本机 Windows、Unity 2022.3.62f3。所有验证使用项目当前规则与正式 SampleScene，未使用 WSL。原始 JSON/JSONL 保存于本目录。

| 证据 | 结果与边界 |
| --- | --- |
| `editmode.json` | job f2b2a637a18e44b0a148cbae9f73c5b2，29/29 通过；规则/原子命令/深复制/快照与 AI |
| `playmode-final.json` | job 63d873c411244c0a87fab62814e8399c，5/5 通过；准备态、注入鼠标、真实场景交互与重开局 |
| `playmode-initial-*.json`、`playmode.json` | 新增测试的初始失败保留：重开局断言误计储备棋视图、选子时序失败；修正为只计 activeSelf、等待初始演出结束，并隔离外部鼠标。最终结果不覆盖这些原始记录 |
| `ai-matches.jsonl` | 玩家数 2/3/4；初始 seed=108+count，骰子 Random seed=71+count，普通决策，最多 1400 个动作；每步检查无重叠和负滑行。155/217/292 个动作、31/39/61 回合均结束；只代表这三个样本 |
| `windows-build.json`、`windows-build-warnings.json` | 最新构建 Succeeded、167526667 bytes、0 errors、3 warnings；3 个既有包/可选资源警告明细未隐藏 |
| `graphics-startup.json`、`graphics-startup.log` | 1280×720、batchmode **启用图形**、Direct3D 11 + RTX 5090；启动检查后停止本次创建的进程。没有人工输入或 Player 截图，不作为玩家视觉验收 |
| `lan-host.jsonl`、`lan-client.jsonl`、`lan-comparison.json` | 最新构建两个独立进程，127.0.0.1:7790，4 方含 2 电脑；客户端断线恢复；25 个共同 revision 的完整快照 SHA256 相同，最终 revision=24，两端 complete=true |
| `lan-host.log`、`lan-client.log` | `-nographics` 探针日志；包含无图形设备导致的 Unsupported/FallbackError shader 提示，不代表图形测试结果。没有检出托管脚本异常；不以该日志声称渲染通过 |
| `scene-bindings.json` | 正式场景 missing=[]、Edit Mode、dirty=false；全项目的可选第三方资源仍可有警告 |
| `distribution.json` | 试玩 ZIP 包含 303 个文件，必要依赖/授权/说明齐全，包内 Draftline.Unity.dll 与最终构建 SHA256 相同 |
| `local-ui-playmode.json` | 反馈后新 job 78b93f3cdbed48efb8e8acbdd4986a13，6/6 通过；增加合并本地入口的混合席位及 LAN 页面往返恢复验证 |
| `local-ui-prefab.json`、`local-ui-build.json` | 合并按钮、七个展开模板、Local=0/Lan=2 的存档值与更新后的 Windows 构建检查；本次 UI 更新没有重新跑首版 29 项核心规则或网络探针 |
| `local-ui-build-warnings.json`、`local-ui-build-warnings-final.json` | 更新过程中一次构建返回未完成编译的警告；确认 Editor Assembly 晚于源文件、编译/导入空闲后重新构建，最终 Succeeded、0 errors / 0 warnings。初始记录保留 |
| `explanation-editmode.json`、`explanation-playmode.json` | 36/36 与 8/8 通过；预览不改变原状态，接触/围杀/墙变原因，未结算围杀条件，原因网络传输，悬停与引导暂停恢复 |
| `explanation-build*.json`、`explanation-lan-*` | 原因协议构建与同机双进程验证；25 个共同状态一致、客户端恢复；最后的说明补充仅重新构建，协议与规则不变 |

行动原因/引导工作跨 2026-10-14–15，沿用此目录归档。最终 explanation-build-final 为 Succeeded、0 errors、0 warnings、Edit Mode、saved=true、missing=0；一次中间构建的未完成编译提示保留在 explanation-build-warnings，确认空闲后已重建。当前 ZIP 内 DLL 与最终构建 SHA256 一致。

`Docs/Review/2026-10-15-rule-guide.png` 与 `2026-10-15-action-reasons.png` 是编辑器可选引导与准备态墙变预览，已检查文字框可容纳内容，预测吃子红圈可见。它们不代表自然完整对局验收。美术资料位于 `Docs/Art`，模型缩略图不代表主题完成效果；第三方模型/材质源文件未改动。

快照摘要包含棋子、道具、库存、控制回合、阶段、滑行、胜者、墙/RNG/回合基线等 MatchSnapshot 字段。摘要一致证明这些离散逻辑状态相同，不证明粒子或动画逐帧同步。一次 loopback 探针不是所有联机流程的覆盖，也不是实际两台设备验收。

渲染截图位于 `Docs/Review`：implemented-menu、implemented-setup、implemented-board-1、1280-preview、1366-preview、topdown-preview。全部来自编辑器 Game View；预览图使用准备的骰子/库存。布局、字体和四角可见性已经审阅，完整操作/美术/音频体验待人工试玩。

试玩反馈后补充 local-menu、local-dropdown、local-difficulty、lan-dropdown 的实际展开状态截图：19 号中文的所需高度 27，原文字框 17，新文字框 36。展开项已目视确认可读，不能只凭收起状态判断它是否正常。自动准备截图时曾在页面刚激活的同一帧调用 Dropdown.Show，UGUI 的 Start 尚未初始化 TweenRunner，工具返回异常；改为等待页面激活后再展开，正常检查通过，没有为此改写运行时 UI。

源代码与 Markdown 的 diff whitespace 检查通过；Unity 保存的 YAML 空字段及生成的 slnx 仍有标准序列化空白，本轮未对整个场景做纯空白重写。最终 UI 悬停说明与字号调整属于表现改动，随后进入最终构建和双进程验证；没有把渲染小改重复纳入规则测试。
