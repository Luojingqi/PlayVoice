using PlayVoice.Resources.Language;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.NotificationPanel;

public class NotificationItem : INotifyPropertyChanged
{
    private string _title;
    private string _message;
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }
    public LabelStatus Status { get; set; } = LabelStatus.None;

    public Storyboard ProgressStoryboard { get; set; }
    public double DurationSeconds { get; }
    public NotificationPanel Owner { get; set; }

    public NotificationItem(string title, string message, LabelStatus status, float dismissSeconds = 5)
    {
        Title = title;
        Message = message;
        Status = status;
        DurationSeconds = dismissSeconds;
    }
    public NotificationItem(Func<string> getTitle, Func<string> getMessage, LabelStatus status, float dismissSeconds = 5)
    {
        Title = getTitle();
        Message = getMessage();
        Status = status;
        DurationSeconds = dismissSeconds;
        this.getTitle = getTitle;
        this.getMessage = getMessage;
        LanguageManager.Inst.CultureChanged += LanguageChanged;
    }

    private void LanguageChanged(System.Globalization.CultureInfo arg1, LanguageManager.LanguageInfo arg2)
    {
        Title = getTitle();
        Message = getMessage();
        Console.WriteLine(Message);
    }

    private Func<string> getTitle;
    private Func<string> getMessage;

    // 暂停进度条动画
    public void PauseAnimation()
    {
        if (ProgressStoryboard != null)
        {
            // 只暂停正在运行且未暂停的动画
            if (ProgressStoryboard.GetCurrentState() != ClockState.Stopped &&
                !ProgressStoryboard.GetIsPaused())
            {
                ProgressStoryboard.Pause();
            }
        }
    }

    // 恢复进度条动画
    public void ResumeAnimation()
    {
        if (ProgressStoryboard != null && ProgressStoryboard.GetIsPaused())
        {
            ProgressStoryboard.Resume();
        }
    }

    public void Close()
    {
        ProgressStoryboard?.Stop();
        Owner?.RemoveNotificationWithAnimation(this);
    }

    public void Dispose()
    {
        if (ProgressStoryboard != null)
        {
            ProgressStoryboard.Stop();
            ProgressStoryboard = null;
        }
        LanguageManager.Inst.CultureChanged -= LanguageChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}