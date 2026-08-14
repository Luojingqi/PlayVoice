using NAudio.CoreAudioApi;
using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using Steamworks;

namespace PlayVoice;

internal sealed record StartupRequirement(string Id, string Message);

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
            new StopAllPlayingPresetFeatureHandler(),
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

    private bool automaticRunEnabled;
    private bool isStarting;
    private bool resumeGoEarAudioAfterStart;
    private bool resumeGoEarInAfterStart;
    private string startupFailureRequirementKey;

    public event Action StartupRequirementsChanged;

    public IReadOnlyList<StartupRequirement> GetIncompleteStartupRequirements()
    {
        var requirements = new List<StartupRequirement>();
        var language = LanguageManager.Inst;

        if (!EquipmentLoder.IsVBCableInstalled())
        {
            requirements.Add(new(
                "VirtualSoundCard",
                $"{language.GetString("虚拟声卡")} · {language.GetString("未安装")}"));
        }

        AddEquipmentRequirement(
            requirements,
            "PhysicalMicrophone",
            "物理麦克风",
            equipment.PhysicalMicrophone,
            false,
            equipment.VirtualMicrophone,
            "虚拟麦克风",
            language);
        AddEquipmentRequirement(
            requirements,
            "PhysicalLoudspeaker",
            "物理扬声器",
            equipment.PhysicalLoudspeaker,
            false,
            equipment.VirtualLoudspeaker,
            "虚拟扬声器",
            language);
        AddEquipmentRequirement(
            requirements,
            "VirtualMicrophone",
            "虚拟麦克风",
            equipment.VirtualMicrophone,
            true,
            equipment.PhysicalMicrophone,
            "物理麦克风",
            language);
        AddEquipmentRequirement(
            requirements,
            "VirtualLoudspeaker",
            "虚拟扬声器",
            equipment.VirtualLoudspeaker,
            true,
            equipment.PhysicalLoudspeaker,
            "物理扬声器",
            language);

        if (equipment.PhysicalMicrophoneState && !IsMicrophoneLoudnessReady())
        {
            requirements.Add(new(
                "MicrophoneLoudness",
                language.GetString("当前麦克风未完成响度测试")));
        }

        if (!string.IsNullOrEmpty(startupFailureRequirementKey))
        {
            requirements.Add(new(
                "AudioServiceFailure",
                language.GetString(startupFailureRequirementKey)));
        }

        return requirements;
    }

    private static void AddEquipmentRequirement(
        ICollection<StartupRequirement> requirements,
        string id,
        string equipmentNameKey,
        MMDevice device,
        bool requiresCable,
        MMDevice conflictingDevice,
        string conflictingEquipmentNameKey,
        LanguageManager language)
    {
        string reason;
        if (device == null)
        {
            reason = language.GetString("未绑定");
        }
        else if (requiresCable && !EquipmentLoder.IsCableEquipment(device))
        {
            reason = language.GetString("必须选择 Cable 设备");
        }
        else if (!requiresCable && EquipmentLoder.IsCableEquipment(device))
        {
            reason = language.GetString("不能选择 Cable 设备");
        }
        else if (conflictingDevice != null && device.ID == conflictingDevice.ID)
        {
            reason =
                $"{language.GetString(conflictingEquipmentNameKey)} · {language.GetString("设备重复")}";
        }
        else return;

        requirements.Add(new(
            id,
            $"{language.GetString(equipmentNameKey)} · {reason}"));
    }

    private bool IsMicrophoneLoudnessReady()
    {
        string microphoneID = equipment.PhysicalMicrophone?.ID;
        return !string.IsNullOrEmpty(microphoneID)
            && config.MicrophoneLufsByDeviceID != null
            && config.MicrophoneLufsByDeviceID.TryGetValue(
                microphoneID, out double microphoneLufs)
            && config.MicrophoneLufs == microphoneLufs
            && string.Equals(
                config.MicrophoneLufsDeviceID, microphoneID, StringComparison.Ordinal);
    }

    private bool AreStartupRequirementsSatisfied()
    {
        return equipment.PhysicalLoudspeakerState
            && EquipmentLoder.IsVBCableInstalled()
            && equipment.PhysicalMicrophoneState
            && equipment.VirtualLoudspeakerState
            && equipment.VirtualMicrophoneState
            && IsMicrophoneLoudnessReady();
    }

    public void EnableAutomaticRun()
    {
        if (automaticRunEnabled) return;
        automaticRunEnabled = true;
        NotifyStartupRequirementsChanged();
    }

    public void NotifyStartupRequirementsChanged()
    {
        startupFailureRequirementKey = null;
        if (automaticRunEnabled && !run)
            TryStartAudio();
        StartupRequirementsChanged?.Invoke();
    }

    public void StopForEquipmentChange()
    {
        if (run)
        {
            resumeGoEarAudioAfterStart = goEar_Audio;
            resumeGoEarInAfterStart = goEar_In;
        }
        TryRun(false);
    }

    public bool TryRun(bool value)
    {
        if (value)
        {
            bool started = TryStartAudio();
            StartupRequirementsChanged?.Invoke();
            return started;
        }

        if (!run) return true;

        run = false;
        bool stoppedSuccessfully = true;
        try
        {
            TryGoEar_In(false);
        }
        catch (Exception exception)
        {
            stoppedSuccessfully = false;
            Console.WriteLine($"[错误] 关闭麦克风耳返失败：{exception}");
        }
        try
        {
            TryGoEar_Audio(false);
        }
        catch (Exception exception)
        {
            stoppedSuccessfully = false;
            Console.WriteLine($"[错误] 关闭音频耳返失败：{exception}");
        }
        try
        {
            audioProxy.Stop();
            RunStateChanged?.Invoke(false);
            return stoppedSuccessfully;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[错误] 停止音频服务失败：{exception}");
            RunStateChanged?.Invoke(false);
            return false;
        }
    }

    private bool TryStartAudio()
    {
        if (run) return true;
        if (isStarting || !AreStartupRequirementsSatisfied()) return false;

        isStarting = true;
        try
        {
            audioProxy.Start();
            run = true;

            bool restoreGoEarAudio = resumeGoEarAudioAfterStart;
            bool restoreGoEarIn = resumeGoEarInAfterStart;

            if (restoreGoEarAudio)
                TryGoEar_Audio(true);
            if (restoreGoEarIn)
                TryGoEar_In(true);

            resumeGoEarAudioAfterStart = false;
            resumeGoEarInAfterStart = false;
            startupFailureRequirementKey = null;
            RunStateChanged?.Invoke(true);
            return true;
        }
        catch (Exception exception)
        {
            run = false;
            startupFailureRequirementKey = "音频服务启动失败，请检查设备";
            Console.WriteLine($"[错误] 启动音频服务失败：{exception}");
            try
            {
                TryGoEar_In(false);
            }
            catch (Exception earException)
            {
                Console.WriteLine($"[错误] 重置麦克风耳返状态失败：{earException}");
            }
            try
            {
                TryGoEar_Audio(false);
            }
            catch (Exception earException)
            {
                Console.WriteLine($"[错误] 重置音频耳返状态失败：{earException}");
            }
            try
            {
                audioProxy.Stop();
            }
            catch (Exception stopException)
            {
                Console.WriteLine($"[错误] 清理启动失败的音频服务时出错：{stopException}");
            }
            RunStateChanged?.Invoke(false);
            return false;
        }
        finally
        {
            isStarting = false;
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
                () => $"{LanguageManager.Inst.GetString("请先完成启动准备")}",
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
                () => $"{LanguageManager.Inst.GetString("请先完成启动准备")}",
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
            AutoMuteStateChanged?.Invoke(autoMute);
        }
    }
    public event Action<bool> AutoMuteStateChanged;

    public GlobalData()
    {
        Inst = this;
        PresetStorage.EnsureInitialized();
        try
        {
            config = ConfigStorage.Load();
        }
        catch
        {
            Console.WriteLine("config加载失败");
            config = new Config();
        }
        NormalizeMicrophoneLoudnessConfig();

        TryDetectInitialSteamLanguage();
        ThemeManager.SwitchTheme(config.Theme);
        LanguageManager.Inst.SetCulture(config.Language);
        autoMute = config.AutoMute;
        InitializeSettingToggleHotkeys();

        activeFunctionPreset = FunctionPresetDataTool.EnsureCurrent(config.ActiveFunctionPresetName);
        config.ActiveFunctionPresetName = activeFunctionPreset?.Name;
        config.Save();

        equipment = new();
        audioProxy = new();
        audioProxy.Init();
        equipment.Init();
    }

    private void InitializeSettingToggleHotkeys()
    {
        config.EarAudioToggleHotkey ??= new();
        config.EarInToggleHotkey ??= new();
        config.AutoMuteToggleHotkey ??= new();

        config.EarAudioToggleHotkey.Callback =
            () => TryGoEar_Audio(!GetGoEar_Audio());
        config.EarInToggleHotkey.Callback =
            () => TryGoEar_In(!GetGoEar_In());
        config.AutoMuteToggleHotkey.Callback =
            () => AutoMute = !AutoMute;

        HotkeyManager.Inst.AddHotkey(config.EarAudioToggleHotkey);
        HotkeyManager.Inst.AddHotkey(config.EarInToggleHotkey);
        HotkeyManager.Inst.AddHotkey(config.AutoMuteToggleHotkey);
    }

    private void NormalizeMicrophoneLoudnessConfig()
    {
        config.MicrophoneLufsByDeviceID ??= new(StringComparer.Ordinal);
        if (!config.MicrophoneLufs.HasValue)
            return;

        string microphoneID = config.MicrophoneLufsDeviceID;
        if (string.IsNullOrWhiteSpace(microphoneID))
            microphoneID = config.PhysicalMicrophoneID;
        if (string.IsNullOrWhiteSpace(microphoneID))
            return;

        bool changed = false;
        if (!config.MicrophoneLufsByDeviceID.ContainsKey(microphoneID))
        {
            config.MicrophoneLufsByDeviceID[microphoneID] = config.MicrophoneLufs.Value;
            changed = true;
        }
        if (!string.Equals(
            config.MicrophoneLufsDeviceID, microphoneID, StringComparison.Ordinal))
        {
            config.MicrophoneLufsDeviceID = microphoneID;
            changed = true;
        }
        if (changed)
            config.Save();
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
