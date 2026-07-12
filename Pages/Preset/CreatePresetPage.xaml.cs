using System.Windows.Controls;

namespace PlayVoice.Pages.Preset
{
    /// <summary>
    /// CreatePresetPage.xaml 的交互逻辑
    /// </summary>
    public partial class CreatePresetPage : Page
    {
        public CreatePresetPage()
        {
            InitializeComponent();
            ButtonGroupListBox.SelectionChanged += ButtonGroupListBox_SelectionChanged;
        }

        private void ButtonGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ButtonGroupListBox.SelectedIndex == -1) return;
            string presetName = PresetNameInputTextBox.Text.Trim();

            if (PresetDataTool.CreatePresetData(presetName, out PresetData presetData))
            {
                PresetPage.Inst.AddPresetPage(presetData);
            }

            ButtonGroupListBox.SelectedIndex = -1;
        }
    }
}
