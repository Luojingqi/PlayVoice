using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PlayVoice.Pages.Playback;
using PlayVoice.Pages;

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
            Loaded += (_, _) => Observe(DataContext as LocalDetailPageItemViewModel);
            Unloaded += (_, _) => Observe(null);
            DataContextChanged += (_, _) => { if (IsLoaded) Observe(DataContext as LocalDetailPageItemViewModel); };
        }

        private LocalDetailPageItemViewModel observed;
        private void Observe(LocalDetailPageItemViewModel item)
        {
            if (observed != null)
            {
                observed.MarkedChanged -= RefreshMark;
                observed.Data.PlaybackChanged -= RefreshProgress;
            }
            observed = item;
            if (observed != null)
            {
                observed.MarkedChanged += RefreshMark;
                observed.Data.PlaybackChanged += RefreshProgress;
            }
            RefreshMark(observed?.Marked == true);
            RefreshProgress();
        }

        private void RefreshMark(bool marked) =>
            SelectCenterPoint.Visibility = marked ? Visibility.Visible : Visibility.Collapsed;

        private void RefreshProgress() =>
            PlaybackAnimation.Synchronize(BackgroundScale, observed?.Data.Snapshot ?? default);

        private void CheckBoxListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CheckBoxListBox.SelectedIndex == -1) return;
            ViewModel.Marked = !ViewModel.Marked;
            CheckBoxListBox.SelectedIndex = -1;
        }

        private void CheckBoxListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.Marked = !ViewModel.Marked;
            CheckBoxListBox.SelectedIndex = -1;
            e.Handled = true;
        }

        private void CheckBoxListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void RowBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource != null
                && (ReferenceEquals(originalSource, CheckBoxListBox)
                    || CheckBoxListBox.IsAncestorOf(originalSource)))
            {
                return;
            }

            ClickRippleEffect.Start(RippleCanvas, e.GetPosition(RippleCanvas));
            GlobalData.Inst.Playback.ToggleWorkshopPreview(ViewModel.Data);
        }

        private void RippleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ClickRippleEffect.UpdateClip(RippleCanvas, e.NewSize);
        }
    }
}
