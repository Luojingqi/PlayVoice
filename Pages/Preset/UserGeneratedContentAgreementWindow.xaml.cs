using PlayVoice.Resources.Language;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

public partial class UserGeneratedContentAgreementWindow : Window
{
    private const string ChineseAgreementPath = "EULA/PlayVoice UGC 上传与 Steam 创意工坊服务协议.md";
    private const string EnglishAgreementPath = "EULA/PlayVoice UGC Upload and Steam Workshop Service Agreement.md";

    public UserGeneratedContentAgreementWindow()
    {
        InitializeComponent();
        AgreementTextBlock.Text = LoadAgreement(
            LanguageManager.Inst.CurrentCulture.Name == "zh-CN"
                ? ChineseAgreementPath
                : EnglishAgreementPath);
    }

    private static string LoadAgreement(string resourcePath)
    {
        var resourceUri = new Uri(
            $"/{typeof(UserGeneratedContentAgreementWindow).Assembly.GetName().Name};component/{resourcePath}",
            UriKind.Relative);
        var resource = Application.GetResourceStream(resourceUri)
            ?? throw new FileNotFoundException($"Embedded agreement resource was not found: {resourcePath}", resourcePath);

        using var reader = new StreamReader(resource.Stream);
        return WebUtility.HtmlDecode(reader.ReadToEnd())
            .Replace("\\.", ".")
            .Replace("\\_", "_");
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
