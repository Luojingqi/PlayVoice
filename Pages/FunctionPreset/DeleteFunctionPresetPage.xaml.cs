using System.Windows;
using System.Windows.Controls;
using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
namespace PlayVoice.Pages.FunctionPreset;
public partial class DeleteFunctionPresetPage : Page
{
    public DeleteFunctionPresetPage() { InitializeComponent(); }
    public void Open() { FunctionPresetList.ItemsSource = FunctionPresetDataTool.GetAll().Where(item => !item.IsDefault).ToArray(); }
    public void RefreshLanguage() => FunctionPresetList.Items.Refresh();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => FunctionPresetList.SelectAll();
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeleteButton != null) DeleteButton.IsEnabled = FunctionPresetList.SelectedItems.Count > 0;
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = FunctionPresetList.SelectedItems.Cast<FunctionPresetData>().ToArray();
        if (selected.Length == 0) return;
        var language = LanguageManager.Inst;
        string names = string.Join("\n", selected.Select(item => item.DisplayName));
        string prompt = string.Format(language.CurrentCulture, language.GetString("删除所选预设确认"), names);
        if (MessageBox.Show(Window.GetWindow(this), prompt, language.GetString("删除"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        foreach (var item in selected)
        {
            try
            {
                bool active = string.Equals(GlobalData.Inst.ActiveFunctionPreset?.Name, item.Name, StringComparison.OrdinalIgnoreCase);
                if (!FunctionPresetDataTool.Delete(item.Name)) throw new System.IO.IOException();
                if (active) GlobalData.Inst.ActiveFunctionPreset = FunctionPresetDataTool.EnsureCurrent(null);
            }
            catch (Exception exception)
            {
                Diagnostics.AppLog.Error(exception, "删除预设失败。", "PresetManagement");
                MainWindow.Inst.AddNotification(() => language.GetString("通知"), () => language.GetString("操作失败"), LabelStatus.Error, 4);
            }
        }
        PresetPage.Inst.RefreshFunctionPresetList();
        Open();
    }
}
