using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using Steamworks;
using System.IO;

namespace PlayVoice;

internal class GlobalData
{
    /// <summary>
    /// 向用户展示的程序版本号。
    /// </summary>
    public static readonly string AppVersion =
        $"v{typeof(GlobalData).Assembly.GetName().Version!.ToString(3)}";

    /// <summary>
    /// 用于持久化数据迁移的内部版本索引。
    /// </summary>
    public const int Version = 8;

    private static readonly Dictionary<string, string> SteamLanguageMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["schinese"] = "zh-CN",
            ["tchinese"] = "zh-TW",
            ["english"] = "en-US",
            ["russian"] = "ru-RU",
            ["spanish"] = "es-ES",
            ["latam"] = "es-ES",
            ["brazilian"] = "pt-BR",
            ["japanese"] = "ja-JP",
            ["koreana"] = "ko-KR",
            ["german"] = "de-DE"
        };

    public static GlobalData Inst { get; private set; }

    private Config config;
    public Config Config => config;

    private Equipment equipment;
    public Equipment Equipment => equipment;

    private AudioProxy audioProxy;
    public AudioProxy AudioProxy => audioProxy;

    private readonly List<IFunctionPresetFeatureHandler> functionPresetFeatures =
        new()
        {
            new AudioHotkeyPresetFeatureHandler(),
            new AudioVolumePresetFeatureHandler()
        };

    private AudioPresetData activeAudioPreset;
    private FunctionPresetData activeFunctionPreset;

    public event Action<AudioPresetData> ActiveAudioPresetChanged;
    public event Action<FunctionPresetData> ActiveFunctionPresetChanged;

    public AudioPresetData ActiveAudioPreset
    {
        get => activeAudioPreset;
        set
        {
            if (ReferenceEquals(activeAudioPreset, value)) return;

            ClearFunctionPresetFeatures();
            activeAudioPreset?.Dispose();
            activeAudioPreset = value;
            config.ActiveAudioPresetName = activeAudioPreset?.Config.Name;
            ApplyFunctionPresetFeatures();
            config.Save();
            ActiveAudioPresetChanged?.Invoke(activeAudioPreset);
        }
    }

    public FunctionPresetData ActiveFunctionPreset
    {
        get => activeFunctionPreset;
        set
        {
            if (ReferenceEquals(activeFunctionPreset, value)) return;

            ClearFunctionPresetFeatures();
            activeFunctionPreset = value;
            config.ActiveFunctionPresetName = activeFunctionPreset?.Name;
            ApplyFunctionPresetFeatures();
            config.Save();
            ActiveFunctionPresetChanged?.Invoke(activeFunctionPreset);
        }
    }

    public async Task RestoreActiveAudioPresetAsync()
    {
        if (activeAudioPreset != null || string.IsNullOrWhiteSpace(config.ActiveAudioPresetName))
            return;

        var restoredPreset = await AudioPresetDataTool.LoadAudioPresetData(config.ActiveAudioPresetName);
        if (restoredPreset == null)
        {
            config.ActiveAudioPresetName = null;
            config.Save();
            return;
        }
        ActiveAudioPreset = restoredPreset;
    }

    public HotkeyData GetAudioHotkey(AudioData audioData, bool create)
    {
        if (activeFunctionPreset == null || audioData?.AudioPreset == null)
            return null;

        return activeFunctionPreset.GetHotkey(
            audioData.AudioPreset.Config.Name, audioData.Config.Id, create);
    }

    public double GetAudioDecibel(AudioData audioData)
    {
        if (activeFunctionPreset == null || audioData?.AudioPreset == null)
            return 0;

        return activeFunctionPreset.GetAudioDecibel(
            audioData.AudioPreset.Config.Name, audioData.Config.Id);
    }

    public void SetAudioDecibel(AudioData audioData, double decibel)
    {
        if (activeFunctionPreset == null || audioData?.AudioPreset == null)
            return;

        activeFunctionPreset.SetAudioDecibel(
            audioData.AudioPreset.Config.Name,
            audioData.Config.Id,
            Math.Clamp(decibel, AudioData.MinDecibel, AudioData.MaxDecibel));
        activeFunctionPreset.Save();
    }

    public void SaveActiveFunctionPreset()
    {
        activeFunctionPreset?.Save();
    }

    public void RebuildActiveHotkeys()
    {
        ClearFunctionPresetFeatures();
        ApplyFunctionPresetFeatures();
    }

    public void RemoveAudioBinding(string audioPresetName, string audioId)
    {
        if (activeFunctionPreset?.RemoveAudioBinding(audioPresetName, audioId) == true)
            activeFunctionPreset.Save();
        FunctionPresetDataTool.RemoveAudioBindings(audioPresetName, audioId);
        RebuildActiveHotkeys();
    }

    public void RemoveAudioPresetBindings(string audioPresetName)
    {
        if (activeFunctionPreset?.RemoveAudioPresetBindings(audioPresetName) == true)
            activeFunctionPreset.Save();
        FunctionPresetDataTool.RemoveAudioPresetBindings(audioPresetName);
        RebuildActiveHotkeys();
    }

    private void ClearFunctionPresetFeatures()
    {
        foreach (var feature in functionPresetFeatures)
            feature.Clear();
    }

    private void ApplyFunctionPresetFeatures()
    {
        foreach (var feature in functionPresetFeatures)
            feature.Apply(activeFunctionPreset, activeAudioPreset);
    }

    public void DisposeAudioPresetForExit()
    {
        ClearFunctionPresetFeatures();
        activeAudioPreset?.Dispose();
        activeAudioPreset = null;
    }

    public bool GetRun() => run;
    public bool TryRun(bool value)
    {
        if (value == run) return true;
        if (value == false)
        {
            run = false;
            TryGoEar_In(false);
            TryGoEar_Audio(false);
            RunStateChanged?.Invoke(false);
            audioProxy.Stop();
            return true;
        }
        else
        {
            if (config.MicrophoneLufs == null)
            {
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("请先通过")} {LanguageManager.Inst.GetString("响度归一化测试")}",
                    Pages.LabelStatus.Warning, 4);
                return false;
            }

            bool b = true;
            b &= equipment.PhysicalLoudspeakerState
                && equipment.PhysicalMicrophoneState
                && equipment.VirtualLoudspeakerState
                && equipment.VirtualMicrophoneState;
            if (b == true)
            {
                run = true;
                audioProxy.Start();
                RunStateChanged?.Invoke(true);
                return true;
            }
            else return false;
        }
    }
    private bool run = false;
    public event Action<bool> RunStateChanged;

    private bool goEar_Audio = false;
    public bool GetGoEar_Audio() => goEar_Audio;
    public bool TryGoEar_Audio(bool value)
    {
        if (value == goEar_Audio) return true;
        if (value == true && run == false)
        {
            MainWindow.Inst.AddNotification(
                () => $"{LanguageManager.Inst.GetString("通知")}",
                () => $"{LanguageManager.Inst.GetString("请先启动程序")}",
                Pages.LabelStatus.Warning, 4);
            return false;
        }
        goEar_Audio = value;
        GoEar_AudioStateChanged?.Invoke(goEar_Audio);
        return true;
    }
    public event Action<bool> GoEar_AudioStateChanged;

    private bool goEar_In = false;
    public bool GetGoEar_In() => goEar_In;
    public bool TryGoEar_In(bool value)
    {
        if (value == goEar_In) return true;
        if (value == true && run == false)
        {
            MainWindow.Inst.AddNotification(
                () => $"{LanguageManager.Inst.GetString("通知")}",
                () => $"{LanguageManager.Inst.GetString("请先启动程序")}",
                Pages.LabelStatus.Warning, 4);
            return false;
        }
        goEar_In = value;
        GoEar_InStateChanged?.Invoke(goEar_In);
        return true;
    }
    public event Action<bool> GoEar_InStateChanged;



    private bool autoMute = false;
    public bool AutoMute
    {
        get => autoMute;
        set
        {
            if (value == autoMute) return;
            autoMute = value;
            config.AutoMute = autoMute;
            config.Save();
        }
    }
    public GlobalData()
    {
        Inst = this;
        PresetStorage.EnsureInitialized();
        config = ConfigStorage.Load();

        TryDetectInitialSteamLanguage();
        ThemeManager.SwitchTheme(config.Theme);
        LanguageManager.Inst.SetCulture(config.Language);
        autoMute = config.AutoMute;

        activeFunctionPreset = FunctionPresetDataTool.EnsureCurrent(config.ActiveFunctionPresetName);
        config.ActiveFunctionPresetName = activeFunctionPreset?.Name;
        config.Save();

        equipment = new();
        audioProxy = new();
        audioProxy.Init();
        equipment.Init();
    }

    private void TryDetectInitialSteamLanguage()
    {
        if (config.HasDetectedInitialSteamLanguage)
            return;

        try
        {
            if (!SteamClient.IsValid)
                SteamClient.Init(4907460);

            string steamLanguage = SteamApps.GameLanguage;
            if (string.IsNullOrWhiteSpace(steamLanguage))
                return;

            config.Language = SteamLanguageMap.TryGetValue(steamLanguage, out string language)
                ? language
                : "en-US";
            config.HasDetectedInitialSteamLanguage = true;
            config.Save();
        }
        catch
        {
            // 首次 Steam 语言检测失败时静默保留原配置，下次启动继续尝试。
        }
    }

    public List<string> CopyAudioPathList = new();
}
