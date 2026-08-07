using PlayVoice.Resources.Language;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Input;

namespace PlayVoice.Pages.Preset;

public partial class UserGeneratedContentAgreementWindow : Window
{
    private const string ChineseUserGeneratedContentAgreementPath = "EULA/简体中文_简体中文/PlayVoice UGC 上传与 Steam 创意工坊服务协议.md";
    private const string EnglishUserGeneratedContentAgreementPath = "EULA/English_英语/PlayVoice UGC Upload and Steam Workshop Service Agreement.md";
    private const string UserGeneratedContentAgreementUrl = "https://store.steampowered.com/eula/4907460_eula_1";

    private static readonly IReadOnlyDictionary<string, string> UserGeneratedContentAgreementPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = ChineseUserGeneratedContentAgreementPath,
            ["zh-TW"] = "EULA/繁體中文_繁体中文/PlayVoice UGC 上傳與 Steam 工作坊服務協議.md",
            ["en-US"] = EnglishUserGeneratedContentAgreementPath,
            ["ru-RU"] = "EULA/Русский_俄语/PlayVoice Соглашение о загрузке пользовательского контента и использовании сервисов Мастерской Steam.md",
            ["es-ES"] = "EULA/Español_西班牙语/PlayVoice Acuerdo de subida de CGU y servicios de Steam Workshop.md",
            ["pt-BR"] = "EULA/Português (Brasil)_巴西葡萄牙语/PlayVoice Contrato de Envio de CGU e Serviço da Oficina Steam.md",
            ["ja-JP"] = "EULA/日本語_日语/PlayVoice UGCアップロードおよびSteamワークショップサービス契約.md",
            ["ko-KR"] = "EULA/한국어_韩语/PlayVoice UGC 업로드 및 Steam 창작마당 서비스 계약.md",
            ["de-DE"] = "EULA/Deutsch_德语/PlayVoice-Vereinbarung über das Hochladen nutzergenerierter Inhalte und die Nutzung des Steam-Workshop-Dienstes.md"
        };

    public UserGeneratedContentAgreementWindow()
    {
        InitializeComponent();

        string agreementPath = UserGeneratedContentAgreementPaths.GetValueOrDefault(
            LanguageManager.Inst.CurrentCulture.Name,
            EnglishUserGeneratedContentAgreementPath);
        AgreementTextBlock.Text = LoadAgreement(agreementPath);
    }

    private static string LoadAgreement(string resourcePath)
    {
        var resourceUri = new Uri(
            $"/{typeof(UserGeneratedContentAgreementWindow).Assembly.GetName().Name};component/{resourcePath}",
            UriKind.Relative);
        var resource = Application.GetResourceStream(resourceUri);
        if (resource == null)
        {
            return LanguageManager.Inst.CurrentCulture.TwoLetterISOLanguageName == "zh"
                ? $"此版本未嵌入 PlayVoice UGC 协议，请通过以下永久链接阅读：\n\n{UserGeneratedContentAgreementUrl}"
                : $"The PlayVoice UGC Agreement is not embedded in this build. Read it at the permanent URL below:\n\n{UserGeneratedContentAgreementUrl}";
        }

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
