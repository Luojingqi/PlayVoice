using PlayVoice.Resources.Language;
using System.Windows.Controls;
using static PlayVoice.Equipment;

namespace PlayVoice.Pages.Setting;

/// <summary>
/// SoundCardBindingPage.xaml 的交互逻辑
/// </summary>
public partial class SoundCardBindingPage : Page
{
    private SoundCardBindingPageViewModel viewModel;



    private SoundCardBindingPageViewModel.ComboBoxItemModel nullItem;

    private SoundCardBindingPageViewModel.DropDownListModel PhysicalLoudspeakerModel;
    private SoundCardBindingPageViewModel.DropDownListModel VirtualLoudspeakerModel;
    private SoundCardBindingPageViewModel.DropDownListModel PhysicalMicrophoneModel;
    private SoundCardBindingPageViewModel.DropDownListModel VirtualMicrophoneModel;
    public SoundCardBindingPage()
    {
        InitializeComponent();
        Unloaded += SoundCardBindingPage_Unloaded;
        viewModel = new SoundCardBindingPageViewModel();
        DataContext = viewModel;
        nullItem = new() { Name = LanguageManager.Inst.GetString("请选择"), ID = "" };
        LanguageManager.Inst.CultureChanged += SetNullItemName;
        var equipment = GlobalData.Inst.Equipment;
        PhysicalLoudspeakerModel = new(this, PhysicalLoudspeakerComboBox, equipment.LoudspeakerDic, nullItem,
            (item) =>
            {
                GlobalData.Inst.Equipment.PhysicalLoudspeaker = item;
            },
            equipment.GetLoudspeaker);
        equipment.PhysicalLoudspeakerOnChanged += PhysicalLoudspeakerModel.OnDeviceChanged;
        equipment.OnLoudspeakerAdd += PhysicalLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerRemove += PhysicalLoudspeakerModel.OnDeviceRemove;
        equipment.PhysicalLoudspeakerStateChange += PhysicalLoudspeakerModel.SetState;
        PhysicalLoudspeakerModel.SetState(equipment.PhysicalLoudspeakerState);
        LanguageManager.Inst.CultureChanged += PhysicalLoudspeakerModel.UpdateLanguage;
        if (equipment.PhysicalLoudspeaker != null)
        {
            int index = 1;
            foreach (var device in equipment.LoudspeakerDic)
                if (device.Key == equipment.PhysicalLoudspeaker.ID)
                {
                    PhysicalLoudspeakerModel.DropDownList.IsSyncing = true;
                    PhysicalLoudspeakerModel.DropDownList.SelectedIndex = index;
                    PhysicalLoudspeakerModel.DropDownList.IsSyncing = false;
                }
                else index++;
        }

        VirtualLoudspeakerModel = new(this, VirtualLoudspeakerComboBox, equipment.LoudspeakerDic, nullItem,
            (item) =>
            {
                GlobalData.Inst.Equipment.VirtualLoudspeaker = item;
            },
            equipment.GetLoudspeaker);
        equipment.VirtualLoudspeakerOnChanged += VirtualLoudspeakerModel.OnDeviceChanged;
        equipment.OnLoudspeakerAdd += VirtualLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerRemove += VirtualLoudspeakerModel.OnDeviceRemove;
        equipment.VirtualLoudspeakerStateChange += VirtualLoudspeakerModel.SetState;
        VirtualLoudspeakerModel.SetState(equipment.VirtualLoudspeakerState);
        LanguageManager.Inst.CultureChanged += VirtualLoudspeakerModel.UpdateLanguage;
        if (equipment.VirtualLoudspeaker != null)
        {
            int index = 1;
            foreach (var device in equipment.LoudspeakerDic)
                if (device.Key == equipment.VirtualLoudspeaker.ID)
                {
                    VirtualLoudspeakerModel.DropDownList.IsSyncing = true;
                    VirtualLoudspeakerModel.DropDownList.SelectedIndex = index;
                    VirtualLoudspeakerModel.DropDownList.IsSyncing = false;
                }
                else index++;
        }

        PhysicalMicrophoneModel = new(this, PhysicalMicrophoneComboBox, equipment.MicrophoneDic, nullItem,
            (item) =>
            {
                GlobalData.Inst.Equipment.PhysicalMicrophone = item;
            },
            equipment.GetMicrophone);
        equipment.PhysicalMicrophoneOnChanged += PhysicalMicrophoneModel.OnDeviceChanged;
        equipment.OnMicrophoneAdd += PhysicalMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneRemove += PhysicalMicrophoneModel.OnDeviceRemove;
        equipment.PhysicalMicrophoneStateChange += PhysicalMicrophoneModel.SetState;
        PhysicalMicrophoneModel.SetState(equipment.PhysicalMicrophoneState);
        LanguageManager.Inst.CultureChanged += PhysicalMicrophoneModel.UpdateLanguage;
        if (equipment.PhysicalMicrophone != null)
        {
            int index = 1;
            foreach (var device in equipment.MicrophoneDic)
                if (device.Key == equipment.PhysicalMicrophone.ID)
                {
                    PhysicalMicrophoneModel.DropDownList.IsSyncing = true;
                    PhysicalMicrophoneModel.DropDownList.SelectedIndex = index;
                    PhysicalMicrophoneModel.DropDownList.IsSyncing = false;
                }
                else index++;
        }


        VirtualMicrophoneModel = new(this, VirtualMicrophoneComboBox, equipment.MicrophoneDic, nullItem,
            (item) =>
            {
                GlobalData.Inst.Equipment.VirtualMicrophone = item;
            },
            equipment.GetMicrophone);
        equipment.VirtualMicrophoneOnChanged += VirtualMicrophoneModel.OnDeviceChanged;
        equipment.OnMicrophoneAdd += VirtualMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneRemove += VirtualMicrophoneModel.OnDeviceRemove;
        equipment.VirtualMicrophoneStateChange += VirtualMicrophoneModel.SetState;
        VirtualMicrophoneModel.SetState(equipment.VirtualMicrophoneState);
        LanguageManager.Inst.CultureChanged += VirtualMicrophoneModel.UpdateLanguage;
        if (equipment.VirtualMicrophone != null)
        {
            int index = 1;
            foreach (var device in equipment.MicrophoneDic)
                if (device.Key == equipment.VirtualMicrophone.ID)
                {
                    VirtualMicrophoneModel.DropDownList.IsSyncing = true;
                    VirtualMicrophoneModel.DropDownList.SelectedIndex = index;
                    VirtualMicrophoneModel.DropDownList.IsSyncing = false;
                }
                else index++;
        }
    }

    private void SetNullItemName(System.Globalization.CultureInfo culture, LanguageManager.LanguageInfo languageInfo)
    {
        nullItem.Name = LanguageManager.Inst.GetString("请选择");
    }
    private void SoundCardBindingPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        LanguageManager.Inst.CultureChanged -= SetNullItemName;
        LanguageManager.Inst.CultureChanged -= PhysicalLoudspeakerModel.UpdateLanguage;
        LanguageManager.Inst.CultureChanged -= VirtualLoudspeakerModel.UpdateLanguage;
        LanguageManager.Inst.CultureChanged -= PhysicalMicrophoneModel.UpdateLanguage;
        LanguageManager.Inst.CultureChanged -= VirtualMicrophoneModel.UpdateLanguage;
        var equipment = GlobalData.Inst.Equipment;
        equipment.PhysicalLoudspeakerStateChange -= PhysicalLoudspeakerModel.SetState;
        equipment.VirtualLoudspeakerStateChange -= VirtualLoudspeakerModel.SetState;
        equipment.PhysicalMicrophoneStateChange -= PhysicalMicrophoneModel.SetState;
        equipment.VirtualMicrophoneStateChange -= VirtualMicrophoneModel.SetState;
    }
}
