using System.Windows.Controls;

namespace PlayVoice.Pages.Preset
{
    /// <summary>
    /// 创建音频预设页面的交互逻辑。
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

            if (AudioPresetDataTool.CreateAudioPresetData(
                presetName, out AudioPresetData presetData))
            {
                PresetPage.Inst.AddAudioPresetPage(presetData);
            }

            ButtonGroupListBox.SelectedIndex = -1;
        }
    }
}
