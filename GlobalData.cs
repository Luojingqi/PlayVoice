using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;
using PlayVoice.Pages.Workshop;
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

    private PresetData presetData;
    private int presetChangeVersion;
    public event Action<PresetData> PresetDataChanged;
    public Task LoadLastPresetTask { get; private set; }

    public PresetData PresetData
    {
        get => presetData;
        set
        {
            if (ReferenceEquals(presetData, value)) return;
            presetChangeVersion++;
            if (presetData != null)
            {
                HotkeyManager.Inst.ClearHotkeys();
                presetData.Dispose();
            }
            presetData = value;
            if (presetData != null)
            {
                foreach (var item in presetData.AudioList)
                {
                    HotkeyManager.Inst.AddHotkey(item.Config.HotkeyData);
                    item.Config.HotkeyData.Callback = () =>
                    {
                        if (run == true)
                            item.Start();
                    };
                }
            }
            config.LastPresetName = presetData?.Config?.Name ?? string.Empty;
            config.Save();
            PresetDataChanged?.Invoke(presetData);
        }
    }

    public void DisposePresetForExit()
    {
        if (presetData == null) return;
        HotkeyManager.Inst.ClearHotkeys();
        presetData.Dispose();
        presetData = null;
    }

    private async Task LoadLastPresetAsync()
    {
        string presetName = config.LastPresetName;
        if (string.IsNullOrWhiteSpace(presetName)) return;

        int changeVersion = presetChangeVersion;
        PresetData loadedPreset = null;
        try
        {
            loadedPreset = await PresetDataTool.LoadPresetData(presetName);
        }
        catch
        {
            // 配置中的预设已损坏或无法读取时，回退到无预设状态。
        }

        if (changeVersion != presetChangeVersion)
        {
            loadedPreset?.Dispose();
            return;
        }

        if (loadedPreset != null)
        {
            PresetData = loadedPreset;
        }
        else
        {
            config.LastPresetName = string.Empty;
            config.Save();
        }
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
            if(config.IsPassVolumeTest == false)
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
            config = new Config();

        if (config.Theme == null)
            ThemeManager.SwitchTheme(ThemeManager.Default);
        else
            ThemeManager.SwitchTheme(config.Theme.Value);

        if (string.IsNullOrEmpty(config.Language))
            LanguageManager.Inst.SetCulture("zh-CN");
        else
            LanguageManager.Inst.SetCulture(config.Language);

        equipment = new();
        audioProxy = new();
        audioProxy.Init();
        equipment.Init();
        LoadLastPresetTask = LoadLastPresetAsync();
    }

    public List<string> CopyAudioPathList = new();
}
