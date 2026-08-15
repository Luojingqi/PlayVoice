using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlayVoice.Pages.Setting
{
    /// <summary>
    /// TutorialPage.xaml 的交互逻辑
    /// </summary>
    public partial class TutorialPage : Page
    {
        public TutorialPage()
        {
            InitializeComponent();
        }

        private void PresetIconsHeader_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (PresetIconsExpandArrow.IsMouseOver) return;

            PresetIconsExpandArrow.IsExpanded =
                !PresetIconsExpandArrow.IsExpanded;
            e.Handled = true;
        }

        private void PresetIconsExpandArrow_ExpandedChanged(
            object sender,
            RoutedPropertyChangedEventArgs<bool> e)
        {
            double currentHeight = PresetIconsPanel.ActualHeight;
            double currentOpacity = PresetIconsPanel.Opacity;
            double targetHeight = e.NewValue ? MeasurePresetIconsContentHeight() : 0;
            double targetOpacity = e.NewValue ? 1 : 0;
            double targetTranslateY = e.NewValue ? 0 : -6;

            PresetIconsPanel.IsHitTestVisible = e.NewValue;
            PresetIconsPanel.Height = e.NewValue ? double.NaN : 0;
            PresetIconsPanel.Opacity = targetOpacity;

            PresetIconsPanel.BeginAnimation(
                HeightProperty,
                CreateExpandAnimation(currentHeight, targetHeight),
                HandoffBehavior.SnapshotAndReplace);
            PresetIconsPanel.BeginAnimation(
                OpacityProperty,
                CreateExpandAnimation(currentOpacity, targetOpacity),
                HandoffBehavior.SnapshotAndReplace);

            if (PresetIconsPanel.RenderTransform is TranslateTransform transform)
            {
                double currentTranslateY = transform.Y;
                transform.Y = targetTranslateY;
                transform.BeginAnimation(
                    TranslateTransform.YProperty,
                    CreateExpandAnimation(currentTranslateY, targetTranslateY),
                    HandoffBehavior.SnapshotAndReplace);
            }
        }

        private double MeasurePresetIconsContentHeight()
        {
            double availableWidth =
                (PresetIconsPanel.Parent as FrameworkElement)?.ActualWidth
                ?? ActualWidth;
            PresetIconsContent.Measure(
                new Size(availableWidth, double.PositiveInfinity));
            return PresetIconsContent.DesiredSize.Height;
        }

        private static DoubleAnimation CreateExpandAnimation(double from, double to)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            };
        }
    }
}
