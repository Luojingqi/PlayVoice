using PlayVoice.Resources.Language;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlayVoice.Pages.Loading
{
    /// <summary>
    /// LoadingAnimationPage.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingAnimationPage : Page
    {
        public LoadingAnimationPage()
        {
            InitializeComponent();
            DataContext = this;
           // Text = LanguageManager.Inst.GetString("开始上传");
             this.Visibility = Visibility.Hidden;
        }

        public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(LoadingAnimationPage));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}
