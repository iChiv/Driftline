# 编辑器鼠标无响应修复

2026-10-15 用户反馈：进入 Play Mode 后鼠标点击无响应。

## 已确认原因

Windows 侧 MCP 读取到以下 Input System 状态：

- 原生 Mouse，deviceId 2，enabled=false，disabledInFrontend=true，disabledInRuntime=true。
- disabledWhileInBackground=false，排除仅因编辑器失焦而暂停设备的情况。
- 测试创建的 Mouse1，deviceId 61，native=false，仍为 Mouse.current。
- EventSystem 与 InputSystemUIInputModule 存在且启用。

旧指针测试直接禁用已启用鼠标、创建虚拟鼠标，在协程清理中移除虚拟设备并恢复旧设备。测试被中断时可能遗留状态，后续测试又将残留设备当作原有设备继续使用，因此测试通过不代表编辑器真实鼠标已恢复。

已立即启用原生鼠标、移除确认属于测试的 deviceId 61，并将原生鼠标恢复为 Mouse.current。

## 持久修复

- BoardPointerTests 改用 Input System 官方 InputTestFixture，隔离并保存／恢复整个输入系统，不再禁用真实设备。
- 官方 fixture 在退出 Play Mode 时也会执行输入恢复，覆盖中断情形。
- 套件结束检查原有设备集合、enabled 状态与 Mouse.current，防止成功测试之后仍遗留虚拟鼠标。
- 新增通过鼠标输入模块点击“本地游戏”和“开始对局”的回归，避免仅直接调用 Button.onClick 而遗漏 UI 输入链路。
- 自动化运行失焦时，仅隔离测试中的 InputSettings 忽略焦点；正式输入配置不变。
- 隔离在 UnitySetUp 的场景加载之前建立。UI 动作使用从保存配置重建的测试副本，并绑定本轮虚拟鼠标，避免读取隔离前的设备。

该修改仅涉及测试程序集与当前编辑器设备状态，不需要修改游戏规则或重新分发现有 Windows 包。

## 最终验证

- [全量 PlayMode 14/14 通过](playmode-input-isolation.json)，包括经过输入模块的菜单点击与原有棋盘交互回归。
- 套件结束的设备集合、启用状态和 Mouse.current 恢复断言通过。
- [测试结束后再次从编辑器读取](input-devices-after-tests.json)：原生 Mouse / Keyboard 均 enabled=true，disabledInFrontend=false；Mouse.current 为原生 Mouse，虚拟鼠标数为 0。
- 测试造成的 EditorSettings / PlayerSettings 临时变化已恢复，两个设置文件没有 Git 差异。
