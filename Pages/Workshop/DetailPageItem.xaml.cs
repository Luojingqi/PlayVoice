using PlayVoice.Audio;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Workshop
{
    /// <summary>
    /// DetailPageItem.xaml 的交互逻辑
    /// </summary>
    public partial class DetailPageItem : UserControl
    {
        public DetailPageItem(ResourceDataConfig.ResourceItem item) : this()
        {
            ItemTitle = item.Name;
            ItemDuration = AudioData.DurationToString(item.Duration);
            ItemSize = AudioData.SizeToString(item.Size);

            this.Dispatcher.BeginInvoke(new Action(() =>
            {

            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public DetailPageItem()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ItemTitleProperty =
            DependencyProperty.Register("ItemTitle", typeof(string), typeof(DetailPageItem), new PropertyMetadata("未命名"));
        public string ItemTitle
        {
            get { return (string)GetValue(ItemTitleProperty); }
            set { SetValue(ItemTitleProperty, value); }
        }

        public static readonly DependencyProperty ItemDurationProperty =
            DependencyProperty.Register("ItemDuration", typeof(string), typeof(DetailPageItem), new PropertyMetadata("-:-"));
        public string ItemDuration
        {
            get { return (string)GetValue(ItemDurationProperty); }
            set { SetValue(ItemDurationProperty, value); }
        }

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register("ItemSize", typeof(string), typeof(DetailPageItem), new PropertyMetadata("- KB"));
        public string ItemSize
        {
            get { return (string)GetValue(ItemSizeProperty); }
            set { SetValue(ItemSizeProperty, value); }
        }

    }
}
