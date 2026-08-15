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
            equipment.TrySetPhysicalLoudspeaker,
            equipment.GetLoudspeaker,
            () => equipment.PhysicalLoudspeaker);
        equipment.PhysicalLoudspeakerOnChanged += PhysicalLoudspeakerModel.OnDeviceChanged;
        equipment.OnLoudspeakerAdd += PhysicalLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerRemove += PhysicalLoudspeakerModel.OnDeviceRemove;
        equipment.PhysicalLoudspeakerStateChange += PhysicalLoudspeakerModel.SetState;
        PhysicalLoudspeakerModel.SetState(equipment.PhysicalLoudspeakerState);
        LanguageManager.Inst.CultureChanged += PhysicalLoudspeakerModel.UpdateLanguage;
        PhysicalLoudspeakerModel.OnDeviceChanged(
            null,
            equipment.PhysicalLoudspeaker);

        VirtualLoudspeakerModel = new(this, VirtualLoudspeakerComboBox, equipment.LoudspeakerDic, nullItem,
            equipment.TrySetVirtualLoudspeaker,
            equipment.GetLoudspeaker,
            () => equipment.VirtualLoudspeaker);
        equipment.VirtualLoudspeakerOnChanged += VirtualLoudspeakerModel.OnDeviceChanged;
        equipment.OnLoudspeakerAdd += VirtualLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerRemove += VirtualLoudspeakerModel.OnDeviceRemove;
        equipment.VirtualLoudspeakerStateChange += VirtualLoudspeakerModel.SetState;
        VirtualLoudspeakerModel.SetState(equipment.VirtualLoudspeakerState);
        LanguageManager.Inst.CultureChanged += VirtualLoudspeakerModel.UpdateLanguage;
        VirtualLoudspeakerModel.OnDeviceChanged(
            null,
            equipment.VirtualLoudspeaker);

        PhysicalMicrophoneModel = new(this, PhysicalMicrophoneComboBox, equipment.MicrophoneDic, nullItem,
            equipment.TrySetPhysicalMicrophone,
            equipment.GetMicrophone,
            () => equipment.PhysicalMicrophone);
        equipment.PhysicalMicrophoneOnChanged += PhysicalMicrophoneModel.OnDeviceChanged;
        equipment.OnMicrophoneAdd += PhysicalMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneRemove += PhysicalMicrophoneModel.OnDeviceRemove;
        equipment.PhysicalMicrophoneStateChange += PhysicalMicrophoneModel.SetState;
        PhysicalMicrophoneModel.SetState(equipment.PhysicalMicrophoneState);
        LanguageManager.Inst.CultureChanged += PhysicalMicrophoneModel.UpdateLanguage;
        PhysicalMicrophoneModel.OnDeviceChanged(
            null,
            equipment.PhysicalMicrophone);


        VirtualMicrophoneModel = new(this, VirtualMicrophoneComboBox, equipment.MicrophoneDic, nullItem,
            equipment.TrySetVirtualMicrophone,
            equipment.GetMicrophone,
            () => equipment.VirtualMicrophone);
        equipment.VirtualMicrophoneOnChanged += VirtualMicrophoneModel.OnDeviceChanged;
        equipment.OnMicrophoneAdd += VirtualMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneRemove += VirtualMicrophoneModel.OnDeviceRemove;
        equipment.VirtualMicrophoneStateChange += VirtualMicrophoneModel.SetState;
        VirtualMicrophoneModel.SetState(equipment.VirtualMicrophoneState);
        LanguageManager.Inst.CultureChanged += VirtualMicrophoneModel.UpdateLanguage;
        VirtualMicrophoneModel.OnDeviceChanged(
            null,
            equipment.VirtualMicrophone);
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
        equipment.PhysicalLoudspeakerOnChanged -= PhysicalLoudspeakerModel.OnDeviceChanged;
        equipment.VirtualLoudspeakerOnChanged -= VirtualLoudspeakerModel.OnDeviceChanged;
        equipment.PhysicalMicrophoneOnChanged -= PhysicalMicrophoneModel.OnDeviceChanged;
        equipment.VirtualMicrophoneOnChanged -= VirtualMicrophoneModel.OnDeviceChanged;
        equipment.OnLoudspeakerAdd -= PhysicalLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerAdd -= VirtualLoudspeakerModel.OnDeviceAdd;
        equipment.OnLoudspeakerRemove -= PhysicalLoudspeakerModel.OnDeviceRemove;
        equipment.OnLoudspeakerRemove -= VirtualLoudspeakerModel.OnDeviceRemove;
        equipment.OnMicrophoneAdd -= PhysicalMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneAdd -= VirtualMicrophoneModel.OnDeviceAdd;
        equipment.OnMicrophoneRemove -= PhysicalMicrophoneModel.OnDeviceRemove;
        equipment.OnMicrophoneRemove -= VirtualMicrophoneModel.OnDeviceRemove;
        equipment.PhysicalLoudspeakerStateChange -= PhysicalLoudspeakerModel.SetState;
        equipment.VirtualLoudspeakerStateChange -= VirtualLoudspeakerModel.SetState;
        equipment.PhysicalMicrophoneStateChange -= PhysicalMicrophoneModel.SetState;
        equipment.VirtualMicrophoneStateChange -= VirtualMicrophoneModel.SetState;
    }
}
