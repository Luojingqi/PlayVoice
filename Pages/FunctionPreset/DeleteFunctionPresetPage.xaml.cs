using PlayVoice.Pages.Preset;
using System.Windows.Controls;

namespace PlayVoice.Pages.FunctionPreset;

public partial class DeleteFunctionPresetPage : Page
{
    public DeleteFunctionPresetPage()
    {
        InitializeComponent();
        ButtonGroupListBox.SelectionChanged += ButtonGroupListBox_SelectionChanged;
    }

    public void Open()
    {
        FunctionPresetComboBox.ItemsSource = FunctionPresetDataTool.GetAll()
            .Where(item => !item.IsDefault)
            .ToList();
        FunctionPresetComboBox.SelectedIndex = FunctionPresetComboBox.Items.Count > 0 ? 0 : -1;
    }

    public void RefreshLanguage()
    {
        FunctionPresetComboBox.Items.Refresh();
    }

    private void ButtonGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ButtonGroupListBox.SelectedIndex == -1) return;

        if (FunctionPresetComboBox.SelectedItem is FunctionPresetData functionPreset)
        {
            bool wasActive = string.Equals(
                GlobalData.Inst.ActiveFunctionPreset?.Name,
                functionPreset.Name,
                StringComparison.OrdinalIgnoreCase);
            if (FunctionPresetDataTool.Delete(functionPreset.Name))
            {
                if (wasActive)
                    GlobalData.Inst.ActiveFunctionPreset =
                        FunctionPresetDataTool.EnsureCurrent(currentName: null);
                PresetPage.Inst.RefreshFunctionPresetList();
            }
        }

        Open();
        ButtonGroupListBox.SelectedIndex = -1;
    }
}
