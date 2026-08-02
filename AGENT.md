# PlayVoice Agent 指南

## 适用范围

本文档适用于整个仓库。修改代码前先阅读相关的 `.xaml` 与 `.xaml.cs`，并遵循现有数据格式、资源键和生命周期约定。除非任务明确要求，不要顺手重构无关代码，也不要改动用户生成的预设或构建产物。

## 项目概览

PlayVoice 是一个仅面向 Windows 的 WPF 桌面应用，用于把本地音频与物理麦克风混合后送入虚拟麦克风，同时支持物理扬声器监听、全局热键、音频响度归一化、系统托盘、Steam 创意工坊、主题与中英文切换。

- 目标框架：`net10.0-windows`
- UI：WPF；系统托盘使用 Windows Forms
- MVVM：`CommunityToolkit.Mvvm`，但项目仍大量使用 WPF code-behind 和事件驱动代码
- 音频：`NAudio 2.3.0`，共享模式 WASAPI
- Steam：外部 `Facepunch.Steamworks.Win64.dll`，Steam App ID 为 `4907460`
- 音频响度：通过 PATH 中的 `ffmpeg` 执行 EBU R128/LUFS 检测
- 进程权限：`app.manifest` 要求管理员权限

## 目录与职责

- `App.xaml` / `App.xaml.cs`：应用启动与退出、全局键鼠 Hook、系统主题变化处理。
- `MainWindow.xaml` / `MainWindow.xaml.cs`：主窗口、页面导航、通知面板、系统托盘和 Steam 初始化。
- `GlobalData.cs`：运行期总状态与单例入口；持有配置、设备、音频代理和当前预设。
- `Config.cs` / `config.json`：全局设置模型及默认配置。实际配置文件位于应用运行目录。
- `Equipment.cs` / `EquipmentLoder.cs`：物理/虚拟录放音设备绑定、轮询与 VB-CABLE 安装。
- `Audio/`：音频文件、混音、重采样、路由、音量和电平计算。
- `Hotkey/`：Win32 低级键盘/鼠标 Hook、热键录制和按键模拟。
- `Pages/Preset/`：本地预设、音轨导入/删除/排序、热键配置和创意工坊上传。
- `Pages/Workshop/`：Steam UGC 查询、下载、订阅、反馈及本地预览。
- `Pages/Setting/`：设备绑定、运行选项、主题、语言、音量测试和虚拟声卡安装。
- `Pages/` 下的其他目录：可复用 WPF 控件及其样式/行为。
- `Resources/Language/`：`.resx` 本地化资源和 `LanguageManager`。
- `Resources/Themes/`、`Resources/Style/`：主题色和控件资源字典。

## 初始化与关键数据流

主窗口构造时的初始化顺序有依赖关系：

1. 创建 `LanguageManager`。
2. 调用 `ThemeManager.Init()`。
3. 创建 `MainViewModel`。
4. 创建 `GlobalData`，加载配置并初始化 `Equipment` 与 `AudioProxy`。
5. 调用 `InitializeComponent()` 并开始页面导航。

不要在未理解依赖的情况下调整该顺序。大量代码通过 `MainWindow.Inst`、`GlobalData.Inst`、`HotkeyManager.Inst`、`LanguageManager.Inst` 和 `AudioProxy.Inst` 访问全局状态。

音频运行链路如下：

- `Equipment` 维护物理扬声器、物理麦克风、虚拟扬声器和虚拟麦克风四个绑定及其有效状态。
- `GlobalData.TryRun(true)` 只有在响度测试通过且四个设备状态均有效时才启动 `AudioProxy`。
- `AudioProxy` 捕获物理麦克风，将麦克风和预设音频混入虚拟扬声器；可按设置把麦克风或音频同时送往物理扬声器监听。
- `AudioData.Start()` 根据当前运行/监听状态创建重采样与音量 Provider，再交给 `AudioProxy` 的 Mixer。
- 第一条虚拟线路音频开始、最后一条结束时，可能模拟配置中的游戏麦克风按键。

设备切换、预设切换和应用退出都会停止当前音频并释放相关对象。修改这些路径时必须保持此行为。

## 持久化与运行目录

本项目会写入 `AppDomain.CurrentDomain.BaseDirectory`，而不是仓库中的固定开发路径：

- `config.json`：全局设置。
- `Resources/Preset/<预设名>/PresetConfig.json`：预设配置及其音频文件。
- `Resources/temp/`：创意工坊上传的临时副本。
- 创意工坊资源使用 `ResourceConfig.json`、`Thumbnail.<扩展名>` 和音频文件。

对配置模型或 JSON 字段的修改必须考虑旧文件兼容性。除非任务明确要求迁移，不要重命名现有字段、中文枚举成员、资源文件或预设目录结构。不要提交本机生成的预设、临时目录、下载的创意工坊内容或 `bin/`、`obj/`。

## 外部依赖与打包约束

开发和运行环境需要满足以下条件：

- 安装 .NET 10 SDK，并在 Windows 上构建。
- NuGet 可还原 `CommunityToolkit.Mvvm 8.4.2` 与 `NAudio 2.3.0`。
- `PlayVoice.csproj` 当前从 `../../DLL/Facepunch.Steamworks.2.5.2/Release/net6.0/Facepunch.Steamworks.Win64.dll` 引用 Steamworks；换机器时先检查该相对路径。
- `ffmpeg` 必须可从 PATH 调用，否则导入音频时的 LUFS 检测无法正常工作。
- 运行目录需要 `steam_api64.dll` 和 `Thumbnail.png`。
- `EquipmentLoder` 会从运行目录查找 `VBCABLE_Driver_Pack45/VBCABLE_Setup_x64.exe` 及证书文件。
- 创意工坊上传会读取运行目录下的 `Resources/ExtendedExplanation`。

上述 VB-CABLE 与扩展说明资源当前没有由项目文件显式复制；涉及发布或安装逻辑时，要同时验证最终输出目录内容。

## 编码约定

- 保持现有 C# 与 XAML 风格；仓库同时存在文件范围命名空间和块命名空间，优先跟随正在修改的文件。
- 新的业务异步方法返回 `Task`/`Task<T>`；`async void` 只用于 WPF 事件处理器。不要继续扩大未等待任务的范围。
- 音频回调、设备轮询和 Steam 异步结果不一定运行在 UI 线程；任何 WPF 控件更新都通过 `Dispatcher`。
- 订阅长生命周期事件时，明确在 `Unloaded`、`Dispose` 或退出路径取消订阅，避免页面重进后重复触发。
- `AudioFileReader`、`WasapiCapture`、`WasapiOut`、`MMDevice`、定时器、托盘图标等对象必须成对停止并释放。
- 修改 `HashSet<AudioData>` 或 Mixer 输入时，避免在枚举集合的同时通过 `Stop()` 间接修改同一集合。
- 路径使用 `Path.Combine`，文件操作前验证存在性；不要假设当前工作目录等于应用运行目录。
- 热键结构使用 Win32 虚拟键码，并区分左右 Ctrl/Alt/Shift/Win 与鼠标中键/侧键；改动时保持录制、匹配和模拟三条路径一致。
- 不要仅为修正拼写而重命名既有 `EquipmentLoder` 等类型；这种改动会扩大差异并影响引用。

## UI、主题与本地化

- 用户可见字符串优先加入 `Resources/Language/Languages.resx` 与 `Languages.en-US.resx`，两份资源保持相同键集合。
- XAML 使用 `{lan:Lan Key=...}`；代码中使用 `LanguageManager.Inst.GetString(...)`。运行时切换语言的视图模型还需响应 `CultureChanged`。
- 颜色和画刷使用 `{DynamicResource ...}` 并复用现有主题键，确保浅色、深色和跟随系统三种模式均可用。
- 可复用控件样式放在 `Resources/Style/`；页面业务布局保留在对应 `Pages/.../*.xaml`。
- 修改页面时同时检查窄窗口、页面重复进入、导航卸载/重载及系统托盘恢复场景。

## 构建与验证

在仓库根目录运行：

```powershell
dotnet restore PlayVoice.slnx
dotnet build PlayVoice.slnx --no-restore
```

当前仓库没有自动化测试项目。每次改动至少执行一次构建；涉及运行时行为时，再按影响范围做手工冒烟测试：

- 启动与退出：UAC、关闭/最小化到托盘、托盘切换预设、会话结束。
- 设备与音频：四类设备绑定、启动/停止、麦克风耳返、音频监听、自动静音和电平显示。
- 预设：创建、导入、LUFS 音量、排序、复制/粘贴、删除、热键触发和重新加载。
- UI：中英文、浅色/深色/系统主题、页面切换及窗口缩放。
- Steam：在 Steam 客户端登录状态下测试查询、订阅、下载、预览和上传；离线/未登录失败应给出通知且不崩溃。
- VB-CABLE：安装/卸载会提权并重启 Windows 音频服务，只在明确授权且适合的测试环境执行。

截至 2026-08-02，使用 .NET SDK `10.0.302` 构建结果为 0 个错误、51 个警告。警告主要来自可空引用、未等待任务和少量过时 API。不要把现有警告误报为本次失败，同时不要引入新的警告；若任务涉及对应代码，应优先消除触及范围内的警告。

## 提交前检查

- 变更只覆盖任务需要的文件，没有提交 `bin/`、`obj/`、`.vs/`、本机配置或用户预设。
- JSON、资源键、Steam 标签和文件名保持向后兼容。
- 音频、设备、Hook、事件和托盘资源在停止/退出/页面卸载路径正确释放。
- 后台回调没有直接访问 WPF 控件。
- 中英文和三种主题均有合理表现。
- `dotnet build PlayVoice.slnx --no-restore` 成功，且没有新增警告。
