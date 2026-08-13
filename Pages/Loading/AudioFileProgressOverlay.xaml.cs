using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.Loading;

public partial class AudioFileProgressOverlay : UserControl
{
    public AudioFileProgressOverlay()
    {
        InitializeComponent();
    }

    public void Show(string title, int totalFiles)
    {
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ProgressScale.ScaleX = 0;
        TitleText.Text = title;
        FileNameText.Text = string.Empty;
        ProgressText.Text = $"0/{totalFiles} · 0%";
        Visibility = Visibility.Visible;
    }

    public void SetProgress(
        double progress,
        string filePath,
        int currentFile,
        int totalFiles)
    {
        progress = Math.Clamp(progress, 0, 1);
        var animation = new DoubleAnimation
        {
            To = progress,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        ProgressScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
        FileNameText.Text = Path.GetFileName(filePath);
        ProgressText.Text =
            $"{currentFile}/{totalFiles} · {(int)Math.Round(progress * 100)}%";
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }
}
