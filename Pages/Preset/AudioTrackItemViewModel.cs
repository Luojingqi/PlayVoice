using PlayVoice.Audio;
using PlayVoice.Pages.FunctionPreset;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayVoice.Pages.Preset;

public class AudioTrackItemViewModel : INotifyPropertyChanged
{
    private string _header;
    public string Header
    {
        get => _header;
        set { _header = value; OnPropertyChanged(); }
    }

    private string _subHeader0;
    public string SubHeader0
    {
        get => _subHeader0;
        set { _subHeader0 = value; OnPropertyChanged(); }
    }

    private string _subHeader1;
    public string SubHeader1
    {
        get => _subHeader1;
        set { _subHeader1 = value; OnPropertyChanged(); }
    }

    private string _hotkey;

    public string Hotkey
    {
        get => _hotkey;
        set { _hotkey = value; OnPropertyChanged(); }
    }

    private TimeSpan _duration;

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            _duration = value;
            SubHeader0 = AudioData.DurationToString(value);
        }
    }

    private double _volumeSliderValue;
    public double VolumeSliderValue
    {
        get => _volumeSliderValue;
        set { _volumeSliderValue = value; OnPropertyChanged(); VolumeSliderValueChange?.Invoke(value); }
    }

    public event Action<double> VolumeSliderValueChange;

    public AudioData Data;
    private bool isDragging;
    public bool IsDragging
    {
        get => isDragging;
        set { isDragging = value; OnPropertyChanged(); }
    }

    private bool marked = false;
    public bool Marked
    {
        get => marked;
        set
        {
            if (marked == value) return;
            marked = value;
            OnPropertyChanged();
        }
    }
    public AudioTrackItemViewModel(AudioData audioData, FunctionPresetAudioBinding binding)
    {
        this.Data = audioData;
        var config = audioData.Config;
        _header = config.FileName + config.FileFormat;
        Duration = audioData.PlaybackDuration;
        _hotkey = binding?.HotkeyData?.ToString() ?? "None";
        _subHeader1 = AudioData.SizeToString(config.Size);
        _volumeSliderValue = AudioData.DecibelToProportion(
            binding?.Decibel ?? 0) * 100;
        VolumeSliderValueChange += (value) =>
        {
            double decibel = AudioData.ProportionToDecibel(value / 100);
            GlobalData.Inst.SetAudioDecibel(audioData, decibel);
            audioData.SetVolume(decibel);
        };
    }

    public void RefreshHotkey()
    {
        Hotkey = GlobalData.Inst.GetAudioHotkey(Data, create: false)?.ToString() ?? "None";
    }

    public void RefreshFunctionPresetValues(FunctionPresetAudioBinding binding)
    {
        var hotkey = binding?.HotkeyData?.ToString() ?? "None";
        if (Hotkey != hotkey) Hotkey = hotkey;
        RefreshVolumeSlider(binding?.Decibel ?? 0);
    }

    internal void RefreshVolumeSlider(double decibel)
    {
        var volume = AudioData.DecibelToProportion(decibel) * 100;
        if (_volumeSliderValue != volume)
        {
            _volumeSliderValue = volume;
            OnPropertyChanged(nameof(VolumeSliderValue));
        }
    }



    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
