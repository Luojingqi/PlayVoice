using System.Windows;
using System.Windows.Input;

namespace PlayVoice.Pages.Setting;

public partial class FirstLaunchWindow : Window
{
    public FirstLaunchWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_OnMouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
