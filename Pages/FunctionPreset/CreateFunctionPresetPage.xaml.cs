using PlayVoice.Pages.Preset;
using PlayVoice.Resources.Language;
using System.Windows.Controls;

namespace PlayVoice.Pages.FunctionPreset;

public partial class CreateFunctionPresetPage : Page
{
    public CreateFunctionPresetPage()
    {
        InitializeComponent();
        ButtonGroupListBox.SelectionChanged += ButtonGroupListBox_SelectionChanged;
    }

    private void ButtonGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ButtonGroupListBox.SelectedIndex == -1) return;

        string presetName = FunctionPresetNameInputTextBox.Text.Trim();
        bool alreadyExists = FunctionPresetDataTool.GetAll().Any(item =>
            string.Equals(item.Name, presetName, StringComparison.OrdinalIgnoreCase));
        if (alreadyExists)
        {
            MainWindow.Inst.AddNotification(
                () => LanguageManager.Inst.GetString("游戏预设已存在"),
                () => LanguageManager.Inst.SpliceString(
                    LanguageManager.Inst.GetString("名称已存在"), presetName),
                LabelStatus.Warning);
        }
        else
        {
            var functionPreset = FunctionPresetDataTool.Create(presetName);
            if (functionPreset != null)
                PresetPage.Inst.AddFunctionPreset(functionPreset);
        }

        ButtonGroupListBox.SelectedIndex = -1;
    }
}
