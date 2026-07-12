using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// LocalDetailPageItem.xaml 的交互逻辑
    /// </summary>
    public partial class LocalDetailPageItem : UserControl
    {
        public LocalDetailPageItemViewModel ViewModel => (LocalDetailPageItemViewModel)this.DataContext;

        public LocalDetailPageItem()
        {
            InitializeComponent();
            CheckBoxListBox.SelectionChanged += CheckBoxListBox_SelectionChanged;
        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ViewModel.MarkedChanged += b =>
            {
                if (b)
                    SelectCenterPoint.Visibility = Visibility.Visible;
                else
                    SelectCenterPoint.Visibility = Visibility.Collapsed;
            };
        }
        private void CheckBoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckBoxListBox.SelectedIndex == -1) return;
            ViewModel.Marked = !ViewModel.Marked;
            CheckBoxListBox.SelectedIndex = -1;
        }

        private void RowBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;

            ViewModel.Data.AnimationPlayAction = PlayClickAnimation;
            ViewModel.Data.AnimationStopAction = ResetAnimation;
            ViewModel.Data.Start_创意工坊播放();
        }


        public void PlayClickAnimation()
        {
            DoubleAnimation scaleAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(((LocalDetailPageItemViewModel)this.DataContext).Duration)
            };

            BackgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        }

        public void ResetAnimation()
        {
            BackgroundScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BackgroundScale.ScaleX = 0;
        }
    }
}
