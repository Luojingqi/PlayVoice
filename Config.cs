using PlayVoice.Resources.Themes;
using System.IO;

namespace PlayVoice;

internal class Config
{
    /// <summary>
    /// 界面主题。
    /// </summary>
    public ThemeManager.ThemeEnum Theme { get; set; } = ThemeManager.ThemeEnum.System;

    /// <summary>
    /// 界面语言的区域性名称。
    /// </summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// 播放预设音频时是否自动静音物理麦克风。
    /// </summary>
    public bool AutoMute { get; set; } = true;

    /// <summary>
    /// 同时播放的最大音频数量。
    /// </summary>
    public int MaxPlayingAudioCount { get; set; } = 2;

    /// <summary>
    /// 关闭主窗口时是否最小化到系统托盘。
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = false;

    /// <summary>
    /// 是否已经选择过主窗口关闭行为。
    /// </summary>
    public bool HasSelectedCloseBehavior { get; set; } = false;

    /// <summary>
    /// 是否已经确认过首次启动教程提示。
    /// </summary>
    public bool HasAcknowledgedTutorialPrompt { get; set; } = false;

    /// <summary>
    /// 输出到虚拟麦克风的音频增益，单位为分贝。
    /// </summary>
    public double AudioOutDecibel { get; set; } = 0;

    /// <summary>
    /// 音频监听增益，单位为分贝。
    /// </summary>
    public double AudioEarDecibel { get; set; } = 0;

    /// <summary>
    /// 物理麦克风输入增益，单位为分贝。
    /// </summary>
    public double MicrophoneInputDecibel { get; set; } = 0;

    /// <summary>
    /// 响度归一化测试得到的麦克风响度，单位为 LUFS。
    /// </summary>
    public double? MicrophoneLufs { get; set; }

    /// <summary>
    /// 所有输出的全局增益，单位为分贝。
    /// </summary>
    public double GlobalDecibel { get; set; } = 0;

    /// <summary>
    /// 是否已经接受用户生成内容上传与 Steam 创意工坊服务协议。
    /// </summary>
    public bool AcceptedUserGeneratedContentAgreement { get; set; } = false;

    /// <summary>
    /// 绑定的物理麦克风设备 ID。
    /// </summary>
    public string PhysicalMicrophoneID { get; set; }

    /// <summary>
    /// 绑定的物理扬声器设备 ID。
    /// </summary>
    public string PhysicalLoudspeakerID { get; set; }

    /// <summary>
    /// 绑定的虚拟麦克风设备 ID。
    /// </summary>
    public string VirtualMicrophoneID { get; set; }

    /// <summary>
    /// 绑定的虚拟扬声器设备 ID。
    /// </summary>
    public string VirtualLoudspeakerID { get; set; }

    /// <summary>
    /// 当前音频预设名称。
    /// </summary>
    public string ActiveAudioPresetName { get; set; }

    /// <summary>
    /// 当前功能预设名称。
    /// </summary>
    public string ActiveFunctionPresetName { get; set; }

    /// <summary>
    /// 是否已经成功完成首次启动时的 Steam 语言检测。
    /// </summary>
    public bool HasDetectedInitialSteamLanguage { get; set; } = false;

    /// <summary>
    /// 将当前配置保存到程序运行目录。
    /// </summary>
    public bool Save()
    {
        return JsonTool.SaveJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), this);
    }
}
