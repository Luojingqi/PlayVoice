using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.NotificationPanel;

/// <summary>
/// NotificationPanel.xaml 的交互逻辑
/// </summary>
public partial class NotificationPanel : UserControl
{
    public ObservableCollection<NotificationItem> NotificationItems { get; } = new();

    public NotificationPanel()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void AddNotification(string title, string message, LabelStatus status = LabelStatus.None, float autoDismissSeconds = 5)
    {
        var item = new NotificationItem(title, message, status, autoDismissSeconds)
        {
            Owner = this
        };
        NotificationItems.Add(item);
    }
    public void AddNotification(Func<string> title, Func<string> message, LabelStatus status = LabelStatus.None, float autoDismissSeconds = 5)
    {
        var item = new NotificationItem(title, message, status, autoDismissSeconds)
        {
            Owner = this
        };
        NotificationItems.Add(item);
    }


    /// <summary>
    /// 退出动画 + 移除通知
    /// </summary>
    public void RemoveNotificationWithAnimation(NotificationItem item)
    {
        var container = Panel.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
        if (container == null) return;

        var rootGrid = FindVisualChild<Grid>(container, null); // 第一个 Grid
        if (rootGrid == null) return;

        var storyboard = new Storyboard();

        // 整体淡出
        var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(opacityAnim, rootGrid);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        // 高度收缩（用 ScaleTransform 模拟）
        var scaleYAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(scaleYAnim, rootGrid);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        // 需要给 Grid 设置 RenderTransform
        rootGrid.RenderTransform = new ScaleTransform(1, 1);
        rootGrid.RenderTransformOrigin = new Point(0.5, 1);
        storyboard.Children.Add(scaleYAnim);

        // 边距收缩
        var marginAnim = new ThicknessAnimation(
            rootGrid.Margin,
            new Thickness(0),
            TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(marginAnim, rootGrid);
        Storyboard.SetTargetProperty(marginAnim, new PropertyPath(FrameworkElement.MarginProperty));
        storyboard.Children.Add(marginAnim);

        storyboard.Completed += (s, e) =>
        {
            NotificationItems.Remove(item);
            item.Dispose();
        };

        storyboard.Begin();
    }

    // 进度条加载时创建并启动进度动画
    private void ProgressBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border progressBarBorder && progressBarBorder.DataContext is NotificationItem item)
        {
            // 查找内部的进度填充 Border 和 ScaleTransform
            var progressFill = progressBarBorder.Child as Border;
            if (progressFill == null) return;

            var scaleTransform = progressFill.RenderTransform as ScaleTransform;
            if (scaleTransform == null) return;

            // 创建动画：ScaleX 从 1 到 0，时长 = 通知存在时间
            var animation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(item.DurationSeconds));
            Storyboard.SetTarget(animation, progressFill);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Completed += (s, args) =>
            {
                // 动画自然结束 → 移除通知
                RemoveNotificationWithAnimation(item);
            };

            // 保存到 item 以便后续暂停/恢复
            item.ProgressStoryboard = storyboard;

            storyboard.Begin();
        }
    }

    // 鼠标悬停暂停进度
    private void NotifyBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.DataContext is NotificationItem item)
            item.PauseAnimation();
    }

    // 鼠标离开恢复进度
    private void NotifyBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.DataContext is NotificationItem item)
            item.ResumeAnimation();
    }

    // 关闭按钮
    private void NotificationClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NotificationItem item)
            item.Close();
    }

    // 可视化树搜索辅助
    private static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && (name == null || element.Name == name))
                return element;

            var result = FindVisualChild<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
}