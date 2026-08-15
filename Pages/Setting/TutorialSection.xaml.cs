using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.Setting;

public partial class TutorialSection : UserControl
{
    private static readonly Duration AnimationDuration =
        new(TimeSpan.FromMilliseconds(200));

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(TutorialSection),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SectionContentProperty =
        DependencyProperty.Register(
            nameof(SectionContent),
            typeof(object),
            typeof(TutorialSection),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TutorialSection),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public TutorialSection()
    {
        InitializeComponent();
        Loaded += TutorialSection_Loaded;
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private void TutorialSection_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyExpandedState(IsExpanded, animate: false);
    }

    private void HeaderPanel_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (SectionExpandArrow.IsMouseOver) return;

        SectionExpandArrow.IsExpanded = !SectionExpandArrow.IsExpanded;
        e.Handled = true;
    }

    private void SectionExpandArrow_ExpandedChanged(
        object sender,
        RoutedPropertyChangedEventArgs<bool> e)
    {
        if (!IsLoaded) return;
        ApplyExpandedState(e.NewValue, animate: true);
    }

    private void ApplyExpandedState(bool isExpanded, bool animate)
    {
        Thickness targetHeaderMargin = isExpanded
            ? new Thickness(0, 1.5, 0, 1.5)
            : new Thickness(0, 10, 0, 10);
        double targetHeight = isExpanded ? MeasureContentHeight() : 0;
        double targetOpacity = isExpanded ? 1 : 0;
        double targetTranslateY = isExpanded ? 0 : -6;

        ContentPanel.IsHitTestVisible = isExpanded;

        if (!animate)
        {
            ClearAnimations();
            HeaderPanel.Margin = targetHeaderMargin;
            ContentPanel.Height = isExpanded ? double.NaN : 0;
            ContentPanel.Opacity = targetOpacity;
            if (ContentPanel.RenderTransform is TranslateTransform initialTransform)
                initialTransform.Y = targetTranslateY;
            return;
        }

        Thickness currentHeaderMargin = HeaderPanel.Margin;
        double currentHeight = ContentPanel.ActualHeight;
        double currentOpacity = ContentPanel.Opacity;

        HeaderPanel.Margin = targetHeaderMargin;
        ContentPanel.Height = isExpanded ? double.NaN : 0;
        ContentPanel.Opacity = targetOpacity;

        HeaderPanel.BeginAnimation(
            MarginProperty,
            CreateAnimation(currentHeaderMargin, targetHeaderMargin),
            HandoffBehavior.SnapshotAndReplace);
        ContentPanel.BeginAnimation(
            HeightProperty,
            CreateAnimation(currentHeight, targetHeight),
            HandoffBehavior.SnapshotAndReplace);
        ContentPanel.BeginAnimation(
            OpacityProperty,
            CreateAnimation(currentOpacity, targetOpacity),
            HandoffBehavior.SnapshotAndReplace);

        if (ContentPanel.RenderTransform is TranslateTransform transform)
        {
            double currentTranslateY = transform.Y;
            transform.Y = targetTranslateY;
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                CreateAnimation(currentTranslateY, targetTranslateY),
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private double MeasureContentHeight()
    {
        double availableWidth = ActualWidth > 0
            ? ActualWidth
            : (Parent as FrameworkElement)?.ActualWidth ?? 0;
        SectionContentPresenter.Measure(
            new Size(availableWidth, double.PositiveInfinity));
        return SectionContentPresenter.DesiredSize.Height;
    }

    private void ClearAnimations()
    {
        HeaderPanel.BeginAnimation(MarginProperty, null);
        ContentPanel.BeginAnimation(HeightProperty, null);
        ContentPanel.BeginAnimation(OpacityProperty, null);
        if (ContentPanel.RenderTransform is TranslateTransform transform)
            transform.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static DoubleAnimation CreateAnimation(double from, double to)
    {
        return new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = AnimationDuration,
            EasingFunction = CreateEasingFunction(),
            FillBehavior = FillBehavior.Stop,
        };
    }

    private static ThicknessAnimation CreateAnimation(
        Thickness from,
        Thickness to)
    {
        return new ThicknessAnimation
        {
            From = from,
            To = to,
            Duration = AnimationDuration,
            EasingFunction = CreateEasingFunction(),
            FillBehavior = FillBehavior.Stop,
        };
    }

    private static QuadraticEase CreateEasingFunction() =>
        new() { EasingMode = EasingMode.EaseOut };
}
