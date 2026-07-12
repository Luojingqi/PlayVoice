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
    public PresetData PresetData
    {
        get => presetData;
        set
        {
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
                        item.Start();
                    };
                }
            }
        }
    }

    public bool GetRun() => run;
    public bool TryRun(bool value)
    {
        if (value == run) return true;
        if (value == false)
        {
            run = false;
            TryGoEar(false);
            RunStateChanged?.Invoke(false);
            audioProxy.Stop();
            return true;
        }
        else
        {
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

    private bool goEar = false;
    public bool GetGoEar() => goEar;
    public bool TryGoEar(bool value)
    {
        if (value == goEar) return true;
        if (value == true && run == false)
        {
            MainWindow.Inst.AddNotification(
                () => $"{LanguageManager.Inst.GetString("通知")}",
                () => $"{LanguageManager.Inst.GetString("请先启动程序")}",
                Pages.LabelStatus.Warning, 4);
            return false;
        }
        goEar = value;
        GoEarStateChanged?.Invoke(goEar);
        return true;
    }

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

    public event Action<bool> GoEarStateChanged;
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
    }

    public List<string> CopyAudioPathList = new();
}
