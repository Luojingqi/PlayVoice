using System.Windows;
using System.Windows.Controls;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
namespace PlayVoice.Pages.Preset;
public partial class DeletePresetPage : Page
{
    public DeletePresetPage() { InitializeComponent(); }
    public void Open() { PresetList.ItemsSource = AudioPresetDataTool.GetAllAudioPresetName(); }
    public void RefreshLanguage() => PresetList.Items.Refresh();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => PresetList.SelectAll();
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeleteButton != null) DeleteButton.IsEnabled = PresetList.SelectedItems.Count > 0;
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = PresetList.SelectedItems.Cast<string>().ToArray();
        if (selected.Length == 0) return;
        var language = LanguageManager.Inst;
        string names = string.Join("\n", selected.Select(item => item));
        string prompt = string.Format(language.CurrentCulture, language.GetString("删除所选预设确认"), names);
        if (MessageBox.Show(Window.GetWindow(this), prompt, language.GetString("删除"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        foreach (var item in selected)
        {
            try
            {
                if (!AudioPresetDataTool.DeleteAudioPresetData(item)) throw new System.IO.IOException();
                PresetPage.Inst.RemoveAudioPresetPage(item, keepManagementPage: true);
            }
            catch (Exception exception)
            {
                Diagnostics.AppLog.Error(exception, "删除预设失败。", "PresetManagement");
                MainWindow.Inst.AddNotification(() => language.GetString("通知"), () => language.GetString("操作失败"), LabelStatus.Error, 4);
            }
        }
        Open();
    }
}
