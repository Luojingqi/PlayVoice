# AGENTS.md

## 项目概览

PlayVoice 是一个仅面向 Windows 的 WPF 桌面应用，用于把本地音频混入虚拟麦克风，并可将麦克风输入及播放音频监听到物理扬声器。项目还包含全局键鼠热键、预设管理、响度控制、多语言、主题以及 Steam 创意工坊相关功能。

- 解决方案：`PlayVoice.slnx`
- 主项目：`PlayVoice.csproj`
- 目标框架：`net10.0-windows`
- UI：WPF / XAML
- 主要依赖：`NAudio 2.3.0`、`CommunityToolkit.Mvvm 8.4.2`
- 外部程序集：`Facepunch.Steamworks.Win64.dll`，当前通过项目外的相对路径引用
- 随程序复制：`config.json`、`steam_api64.dll`、`Thumbnail.png`

## 目录与职责

- `App.xaml(.cs)`：应用启动、全局资源、热键 Hook 生命周期和退出清理。
- `MainWindow.xaml(.cs)` / `MainViewModel.cs`：主窗口、顶层导航和通知入口。
- `GlobalData.cs`：应用级状态与服务入口，协调配置、设备、音频代理、预设和运行状态。
- `Config.cs` / `JsonTool.cs`：用户配置模型及 JSON 持久化；运行时文件位于程序输出目录。
- `Equipment.cs` / `EquipmentLoder.cs`：Windows 音频端点枚举、绑定和设备变化监听。
- `Audio/`：NAudio 音频采集、混音、播放、音量/分贝换算和电平计算。
- `Hotkey/`：Win32 低级键盘/鼠标 Hook、热键录制及按键模拟。
- `Pages/Preset/`：预设、音轨及其磁盘数据。运行时预设位于输出目录的 `Resources/Preset/<预设名>/`。
- `Pages/Workshop/`：Steam 创意工坊列表、详情、本地资源、上传及下载相关 UI 和模型。
- `Pages/Setting/`：系统、声卡绑定、教程、虚拟声卡安装和关于页面。
- `Pages/` 下其他目录：项目自定义 WPF 控件；通常由同名 `.xaml` 和 `.xaml.cs` 组成。
- `Resources/Language/`：`.resx` 本地化资源及语言管理器。
- `Resources/Themes/`、`Resources/Style/`：主题资源字典、控件样式、图标和图片。

## 关键运行关系

1. XAML 资源先初始化 `LanguageManager` 和 `ThemeManager`。
2. `App.OnStartup` 安装全局键鼠 Hook。
3. 主窗口创建 `GlobalData`，后者读取 `config.json`，初始化设备和 `AudioProxy`。
4. `GlobalData.TryRun(true)` 仅在响度测试通过且四个音频端点均有效时启动音频链路。
5. `AudioProxy` 将物理麦克风和预设音频混入虚拟扬声器端点；虚拟声卡再向外暴露虚拟麦克风。
6. 切换预设时会释放旧音轨、清空并重新注册每条音频的热键。

修改初始化顺序、单例或事件订阅前，务必检查 `App.xaml`、`App.xaml.cs`、`MainWindow.xaml.cs` 和 `GlobalData.cs`；多个组件依赖 `*.Inst` 已经创建。

## 构建与验证

在 Windows、.NET 10 SDK 环境中执行：

```powershell
dotnet restore PlayVoice.slnx
dotnet build PlayVoice.slnx
```

注意：

- `Facepunch.Steamworks.Win64.dll` 的 `HintPath` 指向仓库外部的 `..\..\DLL\Facepunch.Steamworks.2.5.2\Release\net6.0\`。缺少该文件时构建可能失败；不要擅自删除引用或提交个人机器的绝对路径。
- 这是 Windows/WPF 项目，音频与 Hook 功能不能在非 Windows 环境可靠验证。
- 仓库当前没有测试项目。修改后至少执行构建，并针对所改功能做手工验证。
- 手工音频验证通常需要物理麦克风、物理扬声器以及被 `EquipmentLoder.IsCableEquipment` 识别的虚拟声卡。
- Steam 创意工坊验证还依赖有效 App ID、Steam 客户端状态及相应本地/账号权限。

推荐的针对性检查：

- XAML/UI：启动应用，切换三个侧栏页面，并检查浅色、深色、系统主题。
- 本地化：同时切换 `zh-CN` 与 `en-US`；新增显示文本时同步维护两个 `.resx` 文件。
- 音频：检查四端点绑定、启动/停止、监听开关、音量滑块、电平条及播放结束后的清理。
- 热键：检查录制、清除、键盘组合键、鼠标中键/侧键以及应用退出后的 Hook 释放。
- 预设：检查创建、加载、删除、音频缺失时的容错以及 `PresetConfig.json` 的兼容性。

## 修改约定

- 保持现有文件作用域命名空间、C# 命名方式和 XAML/code-behind 结构；不要为局部修改进行无关的大规模格式化。
- UI 状态更新必须在 WPF Dispatcher 线程进行；音频回调和设备通知可能来自后台线程。
- NAudio、`MMDevice`、播放器、采集器、流和 Win32 Hook 都需要明确停止或释放。改动停止/切换逻辑时检查重复订阅、悬挂回调和资源泄漏。
- 设备绑定变化会先停止运行。不要绕过 `GlobalData.TryRun` 或 `Equipment` 属性中的状态检查与配置保存。
- 配置和预设 JSON 是用户数据。新增字段应提供安全默认值，并保持旧文件可反序列化；不要在验证过程中覆盖仓库中的示例 `config.json`。
- 预设配置列表和 `PresetData.AudioList` 依赖相同索引对应同一音轨；增删或排序时必须同步维护。
- 新增页面或资源时确认 Build Action 正确，并使用相对 Pack URI。不要手工编辑 `obj/` 或 `bin/` 中的生成文件。
- 用户可见文本应通过 `LanguageManager`/`.resx` 获取，不要只在 XAML 或 C# 中新增单语言硬编码文本。
- P/Invoke、全局 Hook、按键模拟和音频路由属于高风险区域；只做任务所需的最小改动，并保留退出与异常清理路径。

## 工作区安全

- 开始修改前先执行 `git status --short`，保留用户已有改动。
- 不提交 `bin/`、`obj/`、`.vs/`、个人项目设置或运行时生成的预设/配置。
- 不对用户音频、预设目录或配置文件执行递归删除，除非任务明确要求且目标路径已核实。
- 不修改或替换随附的 `steam_api64.dll`，除非任务明确涉及依赖升级。

## 完成标准

- 改动范围与请求一致，没有覆盖无关的用户修改。
- 项目能构建；若受外部 DLL、文件权限或硬件环境阻塞，要在交付时明确说明实际执行的命令和错误。
- UI、音频、热键、配置或预设改动已按上面的相关清单做针对性验证。
- 新增资源、本地化键及 JSON 字段在所有关联文件中保持一致。
