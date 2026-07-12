using PlayVoice.Audio;
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

    private bool marked = false;
    public bool Marked
    {
        get => marked;
        set
        {
            marked = value;
            MarkedChanged?.Invoke(value);
        }
    }
    public event Action<bool> MarkedChanged;
    public AudioTrackItemViewModel(AudioData audioData)
    {
        this.Data = audioData;
        var config = audioData.Config;
        _header = config.FileName + config.FileFormat;
        Duration = audioData.AudioTrackArray[0].TotalTime;
        _hotkey = config.HotkeyData.ToString();
        _subHeader1 = AudioData.SizeToString(config.Size);
        _volumeSliderValue = AudioData.DecibelToProportion(config.Decibel) * 100;
        VolumeSliderValueChange += (value) =>
        {
            config.Decibel = AudioData.ProportionToDecibel(value / 100);
            audioData.Preset.Save();
            audioData.SetVolume(config.Decibel);
        };
    }



    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}