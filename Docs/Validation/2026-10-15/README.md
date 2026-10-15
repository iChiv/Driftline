# 多主题棋盘首版验证

Windows 原生 Unity 2022.3.62f3，MCP http://127.0.0.1:8080/mcp。未使用 WSL。

- EditMode：38/38 通过。保留规则、复制/命令、AI、行动原因测试，新增六主题目录完整性、9 个显式升降绑定、无美术 Collider、材质适配及房间主题包字段验证。
- PlayMode：9/9 通过。新增一项在五个主题中逐张执行真实 InputSystem 鼠标选子与点终点；检查角色原材质保留、对局锁定主题、升降状态和静态合批。合批前后都通过，`themes-playmode.json` 为合批后的最终结果。
- Windows 构建：Succeeded，179,167,364 bytes，0 errors / 0 warnings；详见 `themes-build.json`。直接 execute_code 调用在构建域重载后返回了空 message 的 false，原生 BuildReport 已成功写盘且实际程序验证通过，以归档 BuildReport 为准。
- 双独立进程：房主 `--theme fortress`，客户端 `--theme forest`，端口 7793。revision 0–24 共 25 个状态摘要一致，实际主题均为 fortress；客户端重连成功，本地 preferredTheme 仍为 forest。`themes-lan-comparison.json` 为汇总，原 JSONL 为证据。两进程使用同一个 Windows 用户配置，所以本机偏好储存区共享；该探针验证网络主题不会覆盖客户端偏好，不表示两个本地进程拥有独立系统设置。
- 双进程使用 `-batchmode -nographics`，日志存在无图形设备下的 Shader 支持信息，不能把它当作画面验收。未检出托管异常；探针达到目标后自行退出。
- 独立有图形程序：1280×720 窗口参数，Direct3D 11 / NVIDIA 图形设备，启动日志未检出脚本异常。隐藏窗口未接受 CloseMainWindow，读完日志后已停止本次启动的进程；不代表人工完整试玩。
- 画面：`Docs/Review/2026-10-15-{id}-menu.png` 与 `-play.png` 来自实际 Game View，1920×1080；`-topdown-1366.png` 来自实际 1366×768 Game View。`theme-menu-1280.png` 为实际 1280×720 菜单，`theme-dropdown-live.png` 显示六项可辨文字。棋盘截图为四个人类席位的准备状态，并非完整对局记录。

## 绘制采样

以下是同一编辑器环境、准备对局和 RenderTexture 检查流程中的 UnityStats 快照，包含编辑器额外渲染，不是独立程序 FPS / GPU 性能基准。

| 主题 | 合批前 Draw Calls | 合批后 Draw Calls | 合批后 Batches |
| --- | ---: | ---: | ---: |
| 要塞 | 2475 | 443 | 294 |
| 地城 | 2439 | 433 | 283 |
| 机库 | 2521 | 430 | 283 |
| 森林 | 2470 | 427 | 281 |
| 港口 | 2512 | 437 | 284 |

原始数据为 `theme-render-samples.jsonl` 与 `theme-render-batched.jsonl`。另一次正常编辑器帧的要塞读数约 366 Draw Calls / 277 Batches；不混用它与上表的采样口径。五个主题检查未发现 Error Shader / 空材质；随后查看实际 Game View 确认没有粉色模型。

规则布局未改动；全部场内装饰模型无 Collider，棋子保留独立点击代理，固定环境合批不会合并动态墙/棋子/文字。第三方资产目录的 Git diff 为空。

## 边界与保留记录

真实第二台 Windows 电脑、人工完整联机与长期 GPU/内存采样仍待试玩。本轮没有将这些标为完成。水面为静态风格化平面与水纹，事件特效与音频复用既有版本。

一次临时截图清理操作被工具自动审批拒绝（`blocked by policy`，没有进一步原因），文件保留在工作区。早期 RenderTexture 截图曾出现嵌套下拉 Canvas 遮挡，正式检查改用实际 Game View；该临时 `theme-dropdown.png` 不作为结果、不纳入提交。没有为截图问题改动正式 UI 布局。

正式场景保存/缺失脚本结果见 `scene-final.json`；ZIP 内容和哈希见 `distribution.json`。
