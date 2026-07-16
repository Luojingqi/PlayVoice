using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Audio;
using PlayVoice.Hotkey;
using PlayVoice.Pages.Preset;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static CommunityToolkit.Mvvm.ComponentModel.__Internals.__TaskExtensions.TaskAwaitableWithoutEndValidation;
using static PlayVoice.Audio.AudioProxy;

namespace PlayVoice.Pages.Setting
{
    /// <summary>
    /// SystemSettingsPage.xaml 的交互逻辑
    /// </summary>
    public partial class SystemSettingsPage : Page
    {
        public SystemSettingsPage()
        {
            InitializeComponent();
            LanguageManager.Inst.CultureChanged += UpdateLanguageAction;
            Unloaded += SystemSettingsPage_Unloaded;
            {
                LanguageComboBox.ItemsSource = LanguageManager.Inst.LanguageList;
                LanguageComboBox.DisplayMemberPath = "Value";
                LanguageComboBox.IsSyncing = true;
                LanguageComboBox.SelectedIndex = LanguageManager.Inst.NowLanguageInfo.Index;
                LanguageComboBox.IsSyncing = false;
                LanguageComboBox.OnSelectionChanged += (obj0, obj1) =>
                {
                    var item = (LanguageManager.LanguageInfo)obj1;
                    LanguageManager.Inst.SetCulture(item.Key);
                };
            }

            {
                StyleListBox.ItemsSource = ThemeManager.ThemeList;
                StyleListBox.DisplayMemberPath = "Name";
                StyleListBox.IsSyncing = true;
                StyleListBox.SelectedIndex = ThemeManager.NowTheme.Index;
                StyleListBox.IsSyncing = false;
                StyleListBox.OnSelectionChanged += (obj0, obj1) =>
                {
                    var themeInfo = (ThemeManager.ThemeInfo)obj1;
                    ThemeManager.SwitchTheme(themeInfo.Theme);
                };
                ThemeManager.ThemeChanged += UpdateThemeAction;
            }

            {
                UpdateRunAction(GlobalData.Inst.GetRun());
                RunToggle.OnToggleChanged += (b) =>
                {
                    if (b.Value == true)
                        if (GlobalData.Inst.TryRun(b.Value) == false)
                        {
                            RunToggle.IsSyncing = true;
                            RunToggle.IsChecked = false;
                            RunToggle.IsSyncing = false;
                            MainWindow.Inst.AddNotification(
                               () => LanguageManager.Inst.GetString("通知"),
                               () => $"{LanguageManager.Inst.GetString("软件名称")} {LanguageManager.Inst.GetString("失败")}\n{LanguageManager.Inst.GetString("请查看教程说明重新配置")}",
                               LabelStatus.Error, 3);
                        }
                        else
                        {
                            MainWindow.Inst.AddNotification(
                               () => LanguageManager.Inst.GetString("通知"),
                               () => $"{LanguageManager.Inst.GetString("软件名称")} {LanguageManager.Inst.GetString("启动")}",
                               LabelStatus.Success, 3);
                        }
                    else GlobalData.Inst.TryRun(false);

                };
                GlobalData.Inst.RunStateChanged += UpdateRunAction;
            }


            {
                var tempArray = PresetDataTool.GetAllPresetName();
                presetNameArray.Add(LanguageManager.Inst.GetString("无"));
                int presetIndex = 0;
                for (int i = 0; i < tempArray.Length; i++)
                {
                    presetNameArray.Add(tempArray[i]);
                    if (GlobalData.Inst.PresetData != null && tempArray[i] == GlobalData.Inst.PresetData.Config.Name)
                        presetIndex = i + 1;
                }
                PresetComboBox.ItemsSource = presetNameArray;
                PresetComboBox.IsSyncing = true;
                PresetComboBox.SelectedIndex = presetIndex;
                PresetComboBox.IsSyncing = false;

                bool isSyncing = false;
                PresetComboBox.OnSelectionChanged += async (obj0, obj1) =>
                {
                    if (isSyncing == true) return;
                    isSyncing = true;
                    var presetName = (string)obj1;
                    if (presetName != LanguageManager.Inst.GetString("无"))
                        GlobalData.Inst.PresetData = await PresetDataTool.LoadPresetData(presetName);
                    else
                        GlobalData.Inst.PresetData = null;
                    isSyncing = false;
                };
            }
            {
                UpdateGoEarAudioAction(GlobalData.Inst.GetGoEar_Audio());
                EarAudioToggle.OnToggleChanged += (b) =>
                {
                    if (b.Value == true)
                    {
                        if (GlobalData.Inst.TryGoEar_Audio(b.Value) == false)
                        {
                            EarAudioToggle.IsSyncing = true;
                            EarAudioToggle.IsChecked = false;
                            EarAudioToggle.IsSyncing = false;
                        }
                    }
                    else GlobalData.Inst.TryGoEar_Audio(false);

                };
                GlobalData.Inst.GoEar_AudioStateChanged += UpdateGoEarAudioAction;
            }
            {
                UpdateGoEarInAction(GlobalData.Inst.GetGoEar_In());
                EarInToggle.OnToggleChanged += (b) =>
                {
                    if (b.Value == true)
                    {
                        if (GlobalData.Inst.TryGoEar_In(b.Value) == false)
                        {
                            EarInToggle.IsSyncing = true;
                            EarInToggle.IsChecked = false;
                            EarInToggle.IsSyncing = false;
                        }
                    }
                    else GlobalData.Inst.TryGoEar_In(false);

                };
                GlobalData.Inst.GoEar_InStateChanged += UpdateGoEarInAction;
            }

            {
                VolumeTestGroupListBox.SelectionChanged += (obj0, obj1) =>
                {
                    if (VolumeTestGroupListBox.SelectedIndex == -1) return;
                    if (VolumeTestGroupListBox.SelectedIndex != 0) return;

                    if (physicalMicrophoneCapture != null)
                    {
                        physicalMicrophoneCapture.StopRecording();
                        physicalMicrophoneCapture.Dispose();
                        physicalMicrophoneCapture = null;
                        Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);
                    }

                    var equipment = GlobalData.Inst.Equipment;
                    if (equipment.PhysicalMicrophoneState == false)
                    {
                        MainWindow.Inst.AddNotification(
                            () => $"{LanguageManager.Inst.GetString("通知")}",
                            () => $"{LanguageManager.Inst.GetString("物理麦克风")} {LanguageManager.Inst.GetString("未绑定")}",
                            Pages.LabelStatus.Warning, 4);
                        goto end;
                    }
                    WaveFormat targetFormat = equipment.PhysicalMicrophone.AudioClient.MixFormat;
                    physicalMicrophoneCapture = new WasapiCapture(equipment.PhysicalMicrophone);
                    var physicalMicrophoneWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);
                    physicalMicrophoneCapture.WaveFormat = physicalMicrophoneWaveFormat;
                    var buffer = new BufferedWaveProvider(physicalMicrophoneWaveFormat);
                    float[] sampleBuffer = new float[8192];
                    MeteringSampleProvider meteringSample = null;
                    string testPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources/temp/VolumeTest.wav");
                    var writer = new WaveFileWriter(testPath, physicalMicrophoneWaveFormat);
                    physicalMicrophoneCapture.DataAvailable += (sender, e) =>
                    {
                        buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                        int samplesToRead = e.BytesRecorded / 4;
                        if (sampleBuffer.Length < samplesToRead)
                            sampleBuffer = new float[sampleBuffer.Length * 2];

                        int samplesRead = meteringSample.Read(sampleBuffer, 0, samplesToRead);
                        if (samplesRead > 0)
                        {
                            writer.WriteSamples(sampleBuffer, 0, samplesRead);
                        }
                    };
                    physicalMicrophoneCapture.RecordingStopped += (sender, e) =>
                    {
                        writer.Dispose();
                    };
                    var volumeSample = new VolumeSampleProvider(buffer.ToSampleProvider());
                    volumeSample.Volume = (float)AudioData.DecibelToVolume(GlobalData.Inst.AudioProxy.MicrophoneInputDecibel);
                    meteringSample = new MeteringSampleProvider(volumeSample);
                    SetStreamVolume(meteringSample, SampleEnum.In);
                    physicalMicrophoneCapture.StartRecording();


                    MainWindow.Inst.AddNotification(
                            () => $"{LanguageManager.Inst.GetString("通知")}",
                            () => $"{LanguageManager.Inst.GetString("正在录音")}",
                            Pages.LabelStatus.Warning, 7);
                    Task.Run(async () =>
                    {
                        await Task.Delay(7000);
                        Application.Current?.Dispatcher?.Invoke(async () =>
                        {
                            if (physicalMicrophoneCapture != null)
                            {
                                physicalMicrophoneCapture.StopRecording();
                                physicalMicrophoneCapture.Dispose();
                                physicalMicrophoneCapture = null;
                                Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);

                                MainWindow.Inst.AddNotification(
                                    () => $"{LanguageManager.Inst.GetString("通知")}",
                                    () => $"{LanguageManager.Inst.GetString("录音结束")}",
                                    Pages.LabelStatus.Warning, 3.5f);
                                AudioOutVolumeSlider.Value = AudioData.DecibelToProportion(await AudioData.MeasureLufs(testPath)) * 100;
                                GlobalData.Inst.Config.IsPassVolumeTest = true;
                                GlobalData.Inst.Config.Save();
                            }
                        });
                    });
                end:
                    VolumeTestGroupListBox.SelectedIndex = -1;
                };
            }

            {
                UpdateAutoMuteAction(GlobalData.Inst.AutoMute);
                AutoMuteToggle.OnToggleChanged += (b) =>
                {
                    GlobalData.Inst.AutoMute = b.Value;
                };
            }

            {
                this.AudioOutVolumeSlider.Value = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.AudioOutDecibel) * 100;
                AudioOutVolumeSlider.ValueChanged += (sender, value) =>
                {
                    GlobalData.Inst.AudioProxy.AudioOutDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
            }

            {
                this.AudioEarVolumeSlider.Value = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.AudioEarDecibel) * 100;
                AudioEarVolumeSlider.ValueChanged += (sender, value) =>
                {
                    GlobalData.Inst.AudioProxy.AudioEarDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
            }

            {
                this.MicrophoneVolumeSlider.Value = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.MicrophoneInputDecibel) * 100;
                MicrophoneVolumeSlider.ValueChanged += (sender, value) =>
                {
                    GlobalData.Inst.AudioProxy.MicrophoneInputDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
            }

            {
                this.GlobalVolumeSlider.Value = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.GlobalDecibel) * 100;
                GlobalVolumeSlider.ValueChanged += (sender, value) =>
                {
                    GlobalData.Inst.AudioProxy.GlobalDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
            }

            {
                KeyboardKeyInputTextBox0.Text = GlobalData.Inst.Config.BeforePlayingKey.HotkeyData.ToString();
                KeyboardKeyInputTextBox1.Text = GlobalData.Inst.Config.AfterPlayingKey.HotkeyData.ToString();

                List<PlayAudioKeyDataKeyAction> list0 = new()
                {
                    new (PlayAudioKeyData.KeyAction.按下),
                    new (PlayAudioKeyData.KeyAction.单击),
                };
                List<PlayAudioKeyDataKeyAction> list1 = new()
                {
                    new (PlayAudioKeyData.KeyAction.抬起),
                    new (PlayAudioKeyData.KeyAction.单击),
                };
                BeforePlayingComboBox.ItemsSource = list0;
                BeforePlayingComboBox.DisplayMemberPath = "Name";
                BeforePlayingComboBox.IsSyncing = true;
                BeforePlayingComboBox.SelectedIndex = list0.FindIndex(x => x.keyAction == GlobalData.Inst.Config.BeforePlayingKey.Action);
                BeforePlayingComboBox.IsSyncing = false;
                AfterPlayingComboBox.ItemsSource = list1;
                AfterPlayingComboBox.DisplayMemberPath = "Name";
                AfterPlayingComboBox.IsSyncing = true;
                AfterPlayingComboBox.SelectedIndex = list1.FindIndex(x => x.keyAction == GlobalData.Inst.Config.AfterPlayingKey.Action);
                AfterPlayingComboBox.IsSyncing = false;

                BeforePlayingComboBox.OnSelectionChanged += (item0, item1) =>
                {
                    GlobalData.Inst.Config.BeforePlayingKey.Action = ((PlayAudioKeyDataKeyAction)item1).keyAction;
                    GlobalData.Inst.Config.Save();
                };
                AfterPlayingComboBox.OnSelectionChanged += (item0, item1) =>
                {
                    GlobalData.Inst.Config.AfterPlayingKey.Action = ((PlayAudioKeyDataKeyAction)item1).keyAction;
                    GlobalData.Inst.Config.Save();
                };
            }
        }

        private WasapiCapture physicalMicrophoneCapture;

        public class PlayAudioKeyDataKeyAction
        {
            public string Name { get; set; }

            public PlayAudioKeyData.KeyAction keyAction { get; set; }

            public PlayAudioKeyDataKeyAction(PlayAudioKeyData.KeyAction keyAction)
            {
                Name = LanguageManager.Inst.GetString(keyAction.ToString());
                this.keyAction = keyAction;
            }
        }

        private void SystemSettingsPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            LanguageManager.Inst.CultureChanged -= UpdateLanguageAction;
            ThemeManager.ThemeChanged -= UpdateThemeAction;
            GlobalData.Inst.RunStateChanged -= UpdateRunAction;
            GlobalData.Inst.GoEar_AudioStateChanged -= UpdateGoEarAudioAction;
        }

        private ObservableCollection<string> presetNameArray = new ObservableCollection<string>();


        private void UpdateLanguageAction(System.Globalization.CultureInfo arg1, LanguageManager.LanguageInfo arg2)
        {
            int index = PresetComboBox.SelectedIndex;
            PresetComboBox.IsSyncing = true;
            presetNameArray[0] = LanguageManager.Inst.GetString("无");
            PresetComboBox.SelectedIndex = index;
            PresetComboBox.IsSyncing = false;

            LanguageComboBox.IsSyncing = true;
            LanguageComboBox.SelectedIndex = arg2.Index;
            LanguageComboBox.IsSyncing = false;


            List<PlayAudioKeyDataKeyAction> list0 = new()
                {
                    new (PlayAudioKeyData.KeyAction.按下),
                    new (PlayAudioKeyData.KeyAction.单击),
                };
            List<PlayAudioKeyDataKeyAction> list1 = new()
                {
                    new (PlayAudioKeyData.KeyAction.抬起),
                    new (PlayAudioKeyData.KeyAction.单击),
                };
            BeforePlayingComboBox.IsSyncing = true;
            BeforePlayingComboBox.ItemsSource = list0;
            BeforePlayingComboBox.SelectedIndex = list0.FindIndex(x => x.keyAction == GlobalData.Inst.Config.BeforePlayingKey.Action);
            BeforePlayingComboBox.IsSyncing = false;
            AfterPlayingComboBox.IsSyncing = true;
            AfterPlayingComboBox.ItemsSource = list1;
            AfterPlayingComboBox.SelectedIndex = list1.FindIndex(x => x.keyAction == GlobalData.Inst.Config.AfterPlayingKey.Action);
            AfterPlayingComboBox.IsSyncing = false;
        }

        private void UpdateThemeAction(ThemeManager.ThemeInfo themeInfo)
        {
            StyleListBox.IsSyncing = true;
            StyleListBox.SelectedIndex = themeInfo.Index;
            StyleListBox.IsSyncing = false;
        }

        private void UpdateRunAction(bool b)
        {
            RunToggle.IsSyncing = true;
            RunToggle.IsChecked = b;
            RunToggle.IsSyncing = false;
            if (b)
                RunToggle.LabelStatus = LabelStatus.Success;
            else
                RunToggle.LabelStatus = LabelStatus.None;
        }

        private void UpdateGoEarAudioAction(bool b)
        {
            EarAudioToggle.IsSyncing = true;
            EarAudioToggle.IsChecked = b;
            EarAudioToggle.IsSyncing = false;
        }

        private void UpdateGoEarInAction(bool b)
        {
            EarInToggle.IsSyncing = true;
            EarInToggle.IsChecked = b;
            EarInToggle.IsSyncing = false;
        }

        private void UpdateAutoMuteAction(bool b)
        {
            AutoMuteToggle.IsSyncing = true;
            AutoMuteToggle.IsChecked = b;
            AutoMuteToggle.IsSyncing = false;
        }


        private void KeyboardKeyInputTextBox0_GotFocus(object sender, RoutedEventArgs e)
        {
            HotkeyManager.Inst.StartRecording(OnHotkeyRecorded0);
        }

        private void KeyboardKeyInputTextBox0_LostFocus(object sender, RoutedEventArgs e)
        {
            HotkeyManager.Inst.StopRecording();
        }

        private void OnHotkeyRecorded0(HotkeyData newHotkey)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (newHotkey == null)
                    ClearHotkey(0);
                else
                {
                    var hotkeyData = GlobalData.Inst.Config.BeforePlayingKey.HotkeyData;
                    hotkeyData.Modifiers = newHotkey.Modifiers;
                    hotkeyData.VkCode = newHotkey.VkCode;
                    hotkeyData.IsMouse = newHotkey.IsMouse;

                    KeyboardKeyInputTextBox0.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox0.SelectionStart = KeyboardKeyInputTextBox0.Text.Length;
                }
                Keyboard.Focus(null);
                GlobalData.Inst.Config.Save();
            });
        }

        private void KeyboardKeyInputTextBox1_GotFocus(object sender, RoutedEventArgs e)
        {
            HotkeyManager.Inst.StartRecording(OnHotkeyRecorded1);
        }

        private void KeyboardKeyInputTextBox1_LostFocus(object sender, RoutedEventArgs e)
        {
            HotkeyManager.Inst.StopRecording();
        }

        private void OnHotkeyRecorded1(HotkeyData newHotkey)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (newHotkey == null)
                    ClearHotkey(1);
                else
                {
                    var hotkeyData = GlobalData.Inst.Config.AfterPlayingKey.HotkeyData;
                    hotkeyData.Modifiers = newHotkey.Modifiers;
                    hotkeyData.VkCode = newHotkey.VkCode;
                    hotkeyData.IsMouse = newHotkey.IsMouse;

                    KeyboardKeyInputTextBox1.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox1.SelectionStart = KeyboardKeyInputTextBox1.Text.Length;
                }
                Keyboard.Focus(null);
                GlobalData.Inst.Config.Save();
            });
        }

        private void ClearHotkey(int index)
        {
            HotkeyData hotkeyData = null;
            switch (index)
            {
                case 0:
                    hotkeyData = GlobalData.Inst.Config.BeforePlayingKey.HotkeyData;
                    break;
                case 1:
                    hotkeyData = GlobalData.Inst.Config.AfterPlayingKey.HotkeyData;
                    break;
            }

            hotkeyData.Clear();
            switch (index)
            {
                case 0:
                    KeyboardKeyInputTextBox0.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox0.SelectionStart = KeyboardKeyInputTextBox0.Text.Length;
                    break;
                case 1:
                    KeyboardKeyInputTextBox1.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox1.SelectionStart = KeyboardKeyInputTextBox1.Text.Length;
                    break;
            }
        }
    }
}

