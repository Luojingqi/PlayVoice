using PlayVoice.Audio;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace PlayVoice.Pages.Workshop;

public class LocalDetailPageItemViewModel : INotifyPropertyChanged
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

    public LocalDetailPageItemViewModel(AudioData audioData, ResourceDataConfig.ResourceItem item)
    {
        this.Data = audioData;
        _header = item.FileName + item.FileFormat;
        Duration = audioData.AudioTrackArray[0].TotalTime;
        SubHeader1 = AudioData.SizeToString(item.Size);
    }



    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}