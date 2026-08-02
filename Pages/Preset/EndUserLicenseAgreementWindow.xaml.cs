using PlayVoice.Resources.Language;
using System.Windows;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

public partial class EndUserLicenseAgreementWindow : Window
{
    public EndUserLicenseAgreementWindow()
    {
        InitializeComponent();
        AgreementTextBlock.Text = LanguageManager.Inst.CurrentCulture.Name == "zh-CN"
            ? EndUserLicenseAgreement.zh_CN
            : EndUserLicenseAgreement.en_US;
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void ActionListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ActionListBox.SelectedIndex == -1) return;
        DialogResult = ActionListBox.SelectedIndex == 1;
    }
}
