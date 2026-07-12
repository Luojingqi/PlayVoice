using System.Windows.Controls;

namespace PlayVoice.Pages.Preset
{
    /// <summary>
    /// DeletePresetPage.xaml 的交互逻辑
    /// </summary>
    public partial class DeletePresetPage : Page
    {
        public DeletePresetPage()
        {
            InitializeComponent();
            ButtonGroupListBox.SelectionChanged += ButtonGroupListBox_SelectionChanged;

           
        }

        public void Open()
        {
            var presetNameArray = PresetDataTool.GetAllPresetName();
            PresetComboBox.ItemsSource = presetNameArray;
            if (presetNameArray.Length > 0)
            {
                PresetComboBox.SelectedIndex = 0;
            }
        }

        private void ButtonGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ButtonGroupListBox.SelectedIndex == -1) return;

            var presetName = (string)PresetComboBox.SelectedItem;
            PresetDataTool.DeletePresetData(presetName);
            PresetPage.Inst.RemovePresetPage(presetName);
            Open();
            ButtonGroupListBox.SelectedIndex = -1;
        }
    }
}
