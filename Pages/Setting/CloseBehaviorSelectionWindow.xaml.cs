using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlayVoice.Pages.Setting;

public partial class CloseBehaviorSelectionWindow : Window
{
    public enum CloseBehavior
    {
        DirectClose,
        MinimizeToTray
    }

    public CloseBehavior? SelectedBehavior { get; private set; }

    public CloseBehaviorSelectionWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_OnMouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void ActionListBox_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (ActionListBox.SelectedIndex == -1) return;

        SelectedBehavior = ActionListBox.SelectedIndex == 0
            ? CloseBehavior.DirectClose
            : CloseBehavior.MinimizeToTray;
        DialogResult = true;
    }
}
