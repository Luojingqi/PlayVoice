using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.ToggleSwitchButton
{
    /// <summary>
    /// ToggleSwitchButton.xaml 的交互逻辑
    /// </summary>
    public partial class ToggleSwitchButton : UserControl
    {
        public bool IsSyncing { get; set; } = false;


        public event Action<bool?> OnToggleChanged;

        public ToggleSwitchButton()
        {
            InitializeComponent();
            toggleButton.Click += ToggleButton_Click;
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if(!IsSyncing) OnToggleChanged?.Invoke(toggleButton.IsChecked);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(ToggleSwitchButton), new PropertyMetadata("未命名"));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }


        public static readonly DependencyProperty TextBlockFontSizeProperty =
            DependencyProperty.Register("TextBlockFontSize", typeof(int), typeof(ToggleSwitchButton), new PropertyMetadata(16));
        public int TextBlockFontSize
        {
            get => (int)GetValue(TextBlockFontSizeProperty);
            set => SetValue(TextBlockFontSizeProperty, value);
        }


        public static readonly DependencyProperty ButtonWidthProperty =
            DependencyProperty.Register("ButtonWidth", typeof(double), typeof(ToggleSwitchButton), new PropertyMetadata(80.0));

        public double ButtonWidth
        {
            get => (double)GetValue(ButtonWidthProperty);
            set => SetValue(ButtonWidthProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool?), typeof(ToggleSwitchButton),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool? IsChecked
        {
            get => (bool?)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty LabelStatusProperty =
            DependencyProperty.Register("LabelStatus", typeof(LabelStatus), typeof(ToggleSwitchButton), new PropertyMetadata(LabelStatus.None));

        public LabelStatus LabelStatus
        {
            get => (LabelStatus)GetValue(LabelStatusProperty);
            set => SetValue(LabelStatusProperty, value);
        }


        public static new readonly DependencyProperty PaddingProperty =
           DependencyProperty.Register("Padding", typeof(Thickness), typeof(ToggleSwitchButton), new PropertyMetadata(new Thickness(0)));

        public new Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

    }
}
