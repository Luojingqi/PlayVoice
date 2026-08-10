using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.IO;

namespace PlayVoice;

internal class GlobalData
{
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
            config.ActiveAudioPresetId = activeAudioPreset?.Config.Id;
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
            config.ActiveFunctionPresetId = activeFunctionPreset?.Id;
            ApplyFunctionPresetFeatures();
            config.Save();
            ActiveFunctionPresetChanged?.Invoke(activeFunctionPreset);
        }
    }

    public async Task RestoreActiveAudioPresetAsync()
    {
        if (activeAudioPreset != null || string.IsNullOrWhiteSpace(config.ActiveAudioPresetId))
            return;

        var restoredPreset = await AudioPresetDataTool.LoadAudioPresetData(config.ActiveAudioPresetId);
        if (restoredPreset == null)
        {
            config.ActiveAudioPresetId = null;
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
            audioData.AudioPreset.Config.Id, audioData.Config.Id, create);
    }

    public double GetAudioDecibel(AudioData audioData)
    {
        if (activeFunctionPreset == null || audioData?.AudioPreset == null)
            return 0;

        return activeFunctionPreset.GetAudioDecibel(
            audioData.AudioPreset.Config.Id, audioData.Config.Id);
    }

    public void SetAudioDecibel(AudioData audioData, double decibel)
    {
        if (activeFunctionPreset == null || audioData?.AudioPreset == null)
            return;

        activeFunctionPreset.SetAudioDecibel(
            audioData.AudioPreset.Config.Id,
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

    public void RemoveAudioBinding(string audioPresetId, string audioId)
    {
        if (activeFunctionPreset?.RemoveAudioBinding(audioPresetId, audioId) == true)
            activeFunctionPreset.Save();
        FunctionPresetDataTool.RemoveAudioBindings(audioPresetId, audioId);
        RebuildActiveHotkeys();
    }

    public void RemoveAudioPresetBindings(string audioPresetId)
    {
        if (activeFunctionPreset?.RemoveAudioPresetBindings(audioPresetId) == true)
            activeFunctionPreset.Save();
        FunctionPresetDataTool.RemoveAudioPresetBindings(audioPresetId);
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
        JsonTool.LoadJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), out config);

        if (config == null)
        {
            config = new Config();
            config.Save();
        }

        ThemeManager.SwitchTheme(config.Theme);
        LanguageManager.Inst.SetCulture(config.Language);

        activeFunctionPreset = FunctionPresetDataTool.EnsureCurrent(config.ActiveFunctionPresetId);
        config.ActiveFunctionPresetId = activeFunctionPreset?.Id;
        config.Save();

        equipment = new();
        audioProxy = new();
        audioProxy.Init();
        equipment.Init();
    }

    public List<string> CopyAudioPathList = new();
}
