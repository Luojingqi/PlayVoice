using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using PlayVoice.Hotkey;
using PlayVoice.Pages.FunctionPreset;
using PlayVoice.Pages.Preset;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            Loaded += SystemSettingsPage_Loaded;
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
                RefreshCloseBehaviorOptions();
                CloseBehaviorComboBox.ItemsSource = closeBehaviorOptions;
                CloseBehaviorComboBox.IsSyncing = true;
                CloseBehaviorComboBox.SelectedIndex = GlobalData.Inst.Config.MinimizeToTrayOnClose ? 0 : 1;
                CloseBehaviorComboBox.IsSyncing = false;
                CloseBehaviorComboBox.OnSelectionChanged += (obj0, obj1) =>
                {
                    GlobalData.Inst.Config.MinimizeToTrayOnClose = CloseBehaviorComboBox.SelectedIndex == 0;
                    GlobalData.Inst.Config.HasSelectedCloseBehavior = true;
                    GlobalData.Inst.Config.Save();
                };
            }

            {
                RefreshFunctionPresetOptions();
                FunctionPresetComboBox.OnSelectionChanged += (previous, current) =>
                {
                    if (current is FunctionPresetData functionPreset)
                        GlobalData.Inst.ActiveFunctionPreset = functionPreset;
                };
            }
            {
                var tempArray = AudioPresetDataTool.GetAllAudioPresetName();
                audioPresetNames.Add(LanguageManager.Inst.GetString("无"));
                int presetIndex = 0;
                for (int i = 0; i < tempArray.Length; i++)
                {
                    audioPresetNames.Add(tempArray[i]);
                    if (GlobalData.Inst.ActiveAudioPreset != null
                        && tempArray[i] == GlobalData.Inst.ActiveAudioPreset.Config.Name)
                        presetIndex = i + 1;
                }
                AudioPresetComboBox.ItemsSource = audioPresetNames;
                AudioPresetComboBox.IsSyncing = true;
                AudioPresetComboBox.SelectedIndex = presetIndex;
                AudioPresetComboBox.IsSyncing = false;

                bool isSyncing = false;
                AudioPresetComboBox.OnSelectionChanged += async (obj0, obj1) =>
                {
                    if (isSyncing == true) return;
                    isSyncing = true;
                    var presetName = (string)obj1;
                    if (presetName != LanguageManager.Inst.GetString("无"))
                        GlobalData.Inst.ActiveAudioPreset =
                            await AudioPresetDataTool.LoadAudioPresetData(presetName);
                    else
                        GlobalData.Inst.ActiveAudioPreset = null;
                    isSyncing = false;
                };
            }
            {
                UpdateGoEarAudioAction(GlobalData.Inst.GetGoEar_Audio());
                EarAudioHotkeyInputTextBox.Text =
                    GlobalData.Inst.Config.EarAudioToggleHotkey.ToString();
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
                EarAudioExpandArrow.ExpandedChanged += (_, e) =>
                    AnimateSettingHotkeyPanel(EarAudioHotkeyPanel, e.NewValue);
            }
            {
                UpdateGoEarInAction(GlobalData.Inst.GetGoEar_In());
                EarInHotkeyInputTextBox.Text =
                    GlobalData.Inst.Config.EarInToggleHotkey.ToString();
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
                EarInExpandArrow.ExpandedChanged += (_, e) =>
                    AnimateSettingHotkeyPanel(EarInHotkeyPanel, e.NewValue);
            }

            {
                VolumeTestGroupListBox.SelectionChanged += async (obj0, obj1) =>
                {
                    if (VolumeTestGroupListBox.SelectedIndex == -1) return;
                    if (VolumeTestGroupListBox.SelectedIndex != 0) return;
                    VolumeTestGroupListBox.SelectedIndex = -1;
                    if (isVolumeTestRunning) return;

                    var equipment = GlobalData.Inst.Equipment;
                    if (!equipment.PhysicalMicrophoneState)
                    {
                        MainWindow.Inst.AddNotification(
                            () => $"{LanguageManager.Inst.GetString("通知")}",
                            () => $"{LanguageManager.Inst.GetString("物理麦克风")} {LanguageManager.Inst.GetString("未绑定")}",
                            Pages.LabelStatus.Warning, 4);
                        return;
                    }

                    await RunMicrophoneLoudnessTestAsync();
                };
            }

            {
                UpdateAutoMuteAction(GlobalData.Inst.AutoMute);
                AutoMuteHotkeyInputTextBox.Text =
                    GlobalData.Inst.Config.AutoMuteToggleHotkey.ToString();
                AutoMuteToggle.OnToggleChanged += (b) =>
                {
                    GlobalData.Inst.AutoMute = b.Value;
                };
                GlobalData.Inst.AutoMuteStateChanged += UpdateAutoMuteAction;
                AutoMuteExpandArrow.ExpandedChanged += (_, e) =>
                    AnimateSettingHotkeyPanel(AutoMuteHotkeyPanel, e.NewValue);
            }

            {
                double audioOutVolume = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.AudioOutDecibel) * 100;
                this.AudioVolumeSlider.Value = audioOutVolume;
                this.AudioOutVolumeSlider.Value = audioOutVolume;
                this.AudioEarVolumeSlider.Value = AudioData.DecibelToProportion(GlobalData.Inst.AudioProxy.AudioEarDecibel) * 100;

                AudioVolumeSlider.ValueChanged += (sender, value) =>
                {
                    if (isSyncingAudioVolumeSliders) return;
                    SetAudioVolume(value.NewValue);
                };
                AudioOutVolumeSlider.ValueChanged += (sender, value) =>
                {
                    if (isSyncingAudioVolumeSliders) return;
                    GlobalData.Inst.AudioProxy.AudioOutDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
                AudioEarVolumeSlider.ValueChanged += (sender, value) =>
                {
                    if (isSyncingAudioVolumeSliders) return;
                    GlobalData.Inst.AudioProxy.AudioEarDecibel = AudioData.ProportionToDecibel(value.NewValue / 100);
                };
                AudioVolumeExpandArrow.ExpandedChanged += AudioVolumeExpandArrow_ExpandedChanged;
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
                MaxPlayingAudioCountSlider.Value = GlobalData.Inst.AudioProxy.MaxPlayingAudioCount;
                MaxPlayingAudioCountSlider.ValueChanged += (sender, value) =>
                {
                    GlobalData.Inst.AudioProxy.MaxPlayingAudioCount = (int)value.NewValue;
                };
            }

            {
                RefreshPlayingKeySettings();
                BeforePlayingComboBox.OnSelectionChanged += (item0, item1) =>
                {
                    GlobalData.Inst.ActiveFunctionPreset.BeforePlayingKey.Action =
                        ((PlayAudioKeyDataKeyAction)item1).keyAction;
                    GlobalData.Inst.SaveActiveFunctionPreset();
                };
                AfterPlayingComboBox.OnSelectionChanged += (item0, item1) =>
                {
                    GlobalData.Inst.ActiveFunctionPreset.AfterPlayingKey.Action =
                        ((PlayAudioKeyDataKeyAction)item1).keyAction;
                    GlobalData.Inst.SaveActiveFunctionPreset();
                };
            }
        }

        private bool isVolumeTestRunning;
        private bool isSyncingAudioVolumeSliders;

        private async Task RunMicrophoneLoudnessTestAsync()
        {
            isVolumeTestRunning = true;
            VolumeTestGroupListBox.IsEnabled = false;

            WasapiCapture capture = null;
            WaveFileWriter writer = null;
            bool captureStarted = false;
            bool analysisSucceeded = false;

            try
            {
                var equipment = GlobalData.Inst.Equipment;
                string testedMicrophoneID = equipment.PhysicalMicrophone.ID;
                string testPath = Path.Combine(
                    PresetStorage.TempPath, "VolumeTest.wav");

                capture = new WasapiCapture(equipment.PhysicalMicrophone);
                var microphoneBuffer = new MicrophoneSampleBuffer(
                    capture.WaveFormat,
                    readFully: false,
                    discardOnBufferOverflow: false,
                    bufferDuration: TimeSpan.FromSeconds(1));
                WaveFormat microphoneWaveFormat = microphoneBuffer.SampleFormat;
                int sampleBufferLength = Math.Max(
                    microphoneWaveFormat.Channels,
                    8192 - 8192 % microphoneWaveFormat.Channels);
                float[] sampleBuffer = new float[sampleBufferLength];
                Exception captureProcessingException = null;
                var captureProcessingFailed = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var recordingStopped = new TaskCompletionSource<StoppedEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var volumeSample = new VolumeSampleProvider(
                    microphoneBuffer.SampleProvider)
                {
                    Volume = (float)AudioData.DecibelToVolume(
                        GlobalData.Inst.AudioProxy.MicrophoneInputDecibel)
                };
                var meteringSample = new MeteringSampleProvider(volumeSample);
                SetStreamVolume(meteringSample, SampleEnum.In);
                writer = new WaveFileWriter(testPath, microphoneWaveFormat);
                AppLog.Information(
                    $"开始麦克风响度采集。" +
                    $"DeviceId={testedMicrophoneID} | " +
                    $"Capture={microphoneBuffer.CaptureFormat} | " +
                    $"Sample={microphoneWaveFormat}",
                    "SystemSettings.LoudnessTest");
                capture.RecordingStopped += (sender, e) =>
                    recordingStopped.TrySetResult(e);
                capture.DataAvailable += (sender, e) =>
                {
                    if (Volatile.Read(ref captureProcessingException) != null)
                        return;

                    try
                    {
                        int samplesRemaining = microphoneBuffer.AddSamples(
                            e.Buffer,
                            0,
                            e.BytesRecorded);
                        while (samplesRemaining > 0)
                        {
                            int samplesRequested = Math.Min(
                                sampleBuffer.Length,
                                samplesRemaining);
                            int samplesRead = meteringSample.Read(
                                sampleBuffer,
                                0,
                                samplesRequested);
                            if (samplesRead <= 0)
                            {
                                throw new InvalidDataException(
                                    $"麦克风采集缓冲未返回预期样本。" +
                                    $" Requested={samplesRequested}; " +
                                    $"Remaining={samplesRemaining}");
                            }

                            writer.WriteSamples(sampleBuffer, 0, samplesRead);
                            samplesRemaining -= samplesRead;
                        }
                    }
                    catch (Exception exception)
                    {
                        if (Interlocked.CompareExchange(
                            ref captureProcessingException,
                            exception,
                            null) == null)
                        {
                            captureProcessingFailed.TrySetResult(true);
                        }
                    }
                };

                capture.StartRecording();
                captureStarted = true;
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("正在录音")}",
                    Pages.LabelStatus.Warning, 7);

                Task completedTask = await Task.WhenAny(
                    Task.Delay(7000),
                    captureProcessingFailed.Task,
                    recordingStopped.Task);

                if (completedTask != recordingStopped.Task)
                    capture.StopRecording();
                StoppedEventArgs recordingResult = await recordingStopped.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
                captureStarted = false;
                capture.Dispose();
                capture = null;
                if (recordingResult.Exception != null)
                {
                    throw new InvalidOperationException(
                        "麦克风在响度采集期间停止。",
                        recordingResult.Exception);
                }
                Exception processingException = Volatile.Read(
                    ref captureProcessingException);
                if (processingException != null)
                {
                    throw new InvalidOperationException(
                        "处理麦克风采集数据失败。",
                        processingException);
                }
                writer.Dispose();
                writer = null;
                Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);

                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("录音结束")}",
                    Pages.LabelStatus.Warning, 3.5f);

                using var loudnessTimeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(30));
                double? microphoneLufs = await AudioData.MeasureLufs(
                    testPath,
                    loudnessTimeout.Token);
                if (microphoneLufs.HasValue)
                {
                    var config = GlobalData.Inst.Config;
                    double? previousMicrophoneLufs = config.MicrophoneLufs;
                    string previousMicrophoneLufsDeviceID = config.MicrophoneLufsDeviceID;
                    config.MicrophoneLufsByDeviceID ??= new(StringComparer.Ordinal);
                    bool hadPreviousDeviceResult =
                        config.MicrophoneLufsByDeviceID.TryGetValue(
                            testedMicrophoneID, out double previousDeviceResult);
                    config.MicrophoneLufsByDeviceID[testedMicrophoneID] = microphoneLufs.Value;

                    bool testedMicrophoneIsStillSelected = string.Equals(
                        equipment.PhysicalMicrophone?.ID,
                        testedMicrophoneID,
                        StringComparison.Ordinal);
                    if (testedMicrophoneIsStillSelected)
                    {
                        config.MicrophoneLufs = microphoneLufs.Value;
                        config.MicrophoneLufsDeviceID = testedMicrophoneID;
                    }
                    analysisSucceeded = config.Save();
                    if (!analysisSucceeded)
                    {
                        if (hadPreviousDeviceResult)
                        {
                            config.MicrophoneLufsByDeviceID[testedMicrophoneID] =
                                previousDeviceResult;
                        }
                        else
                        {
                            config.MicrophoneLufsByDeviceID.Remove(testedMicrophoneID);
                        }
                        config.MicrophoneLufs = previousMicrophoneLufs;
                        config.MicrophoneLufsDeviceID = previousMicrophoneLufsDeviceID;
                    }
                }
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "响度测试失败。", "SystemSettings.LoudnessTest");
            }
            finally
            {
                if (capture != null)
                {
                    if (captureStarted)
                    {
                        try
                        {
                            capture.StopRecording();
                        }
                        catch (Exception exception)
                        {
                            AppLog.Error(exception, "停止响度测试录音失败。", "SystemSettings.LoudnessTest");
                        }
                    }

                    try
                    {
                        capture.Dispose();
                    }
                    catch (Exception exception)
                    {
                        AppLog.Error(exception, "释放响度测试录音设备失败。", "SystemSettings.LoudnessTest");
                    }
                }

                try
                {
                    writer?.Dispose();
                }
                catch (Exception exception)
                {
                    AppLog.Error(exception, "释放响度测试录音文件失败。", "SystemSettings.LoudnessTest");
                }

                try
                {
                    Vol(MainWindow.Inst.IL, MainWindow.Inst.IR, 0, 0);
                }
                catch (Exception exception)
                {
                    AppLog.Error(exception, "重置响度测试录音电平失败。", "SystemSettings.LoudnessTest");
                }

                isVolumeTestRunning = false;
                VolumeTestGroupListBox.IsEnabled = true;
                VolumeTestGroupListBox.SelectedIndex = -1;
            }

            if (analysisSucceeded)
            {
                GlobalData.Inst.NotifyStartupRequirementsChanged();
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("分析结束")}",
                    Pages.LabelStatus.Success, 4);
            }
            else
            {
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("响度分析失败，请重试")}",
                    Pages.LabelStatus.Error, 4);
            }
        }

        private void AudioVolumeExpandArrow_ExpandedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (!e.NewValue)
                SetAudioVolume(AudioOutVolumeSlider.Value);

            AnimateAudioVolumePanels(e.NewValue);
        }

        private void AnimateAudioVolumePanels(bool isExpanded)
        {
            AudioVolumePanel.IsHitTestVisible = !isExpanded;
            AudioOutVolumePanel.IsHitTestVisible = isExpanded;
            AudioEarVolumePanel.IsHitTestVisible = isExpanded;

            AnimateOpacity(AudioVolumePanel, isExpanded ? 0 : 1);
            AnimateTranslateY(AudioVolumePanel, isExpanded ? -4 : 0);
            AnimateOpacity(AudioOutVolumePanel, isExpanded ? 1 : 0);
            AnimateTranslateY(AudioOutVolumePanel, isExpanded ? 0 : 4);
            AnimateOpacity(AudioEarVolumePanel, isExpanded ? 1 : 0);
            AnimateTranslateY(AudioEarVolumePanel, isExpanded ? 0 : -6);
            AnimateHeight(AudioEarVolumePanel, isExpanded ? 40.5 : 0);
        }

        private static void AnimateSettingHotkeyPanel(
            FrameworkElement panel,
            bool isExpanded)
        {
            panel.IsHitTestVisible = isExpanded;
            AnimateOpacity(panel, isExpanded ? 1 : 0);
            AnimateTranslateY(panel, isExpanded ? 0 : -6);
            AnimateHeight(panel, isExpanded ? 40.5 : 0);
        }

        private static void AnimateOpacity(UIElement element, double to)
        {
            element.BeginAnimation(
                OpacityProperty,
                CreateAudioVolumeAnimation(element.Opacity, to),
                HandoffBehavior.SnapshotAndReplace);
        }

        private static void AnimateTranslateY(UIElement element, double to)
        {
            if (element.RenderTransform is not TranslateTransform transform) return;
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                CreateAudioVolumeAnimation(transform.Y, to),
                HandoffBehavior.SnapshotAndReplace);
        }

        private static void AnimateHeight(FrameworkElement element, double to)
        {
            element.BeginAnimation(
                HeightProperty,
                CreateAudioVolumeAnimation(element.Height, to),
                HandoffBehavior.SnapshotAndReplace);
        }

        private static DoubleAnimation CreateAudioVolumeAnimation(double from, double to)
        {
            return new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
        }

        private void SetAudioVolume(double value)
        {
            isSyncingAudioVolumeSliders = true;
            AudioVolumeSlider.Value = value;
            AudioOutVolumeSlider.Value = value;
            AudioEarVolumeSlider.Value = value;
            isSyncingAudioVolumeSliders = false;

            double decibel = AudioData.ProportionToDecibel(value / 100);
            GlobalData.Inst.AudioProxy.AudioOutDecibel = decibel;
            GlobalData.Inst.AudioProxy.AudioEarDecibel = decibel;
        }

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
            GlobalData.Inst.ActiveAudioPresetChanged -= UpdateAudioPresetSelection;
            GlobalData.Inst.ActiveFunctionPresetChanged -= UpdateFunctionPresetSelection;
            GlobalData.Inst.GoEar_AudioStateChanged -= UpdateGoEarAudioAction;
            GlobalData.Inst.GoEar_InStateChanged -= UpdateGoEarInAction;
            GlobalData.Inst.AutoMuteStateChanged -= UpdateAutoMuteAction;
        }

        private void SystemSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LanguageManager.Inst.CultureChanged -= UpdateLanguageAction;
            LanguageManager.Inst.CultureChanged += UpdateLanguageAction;
            GlobalData.Inst.ActiveAudioPresetChanged -= UpdateAudioPresetSelection;
            GlobalData.Inst.ActiveAudioPresetChanged += UpdateAudioPresetSelection;
            GlobalData.Inst.ActiveFunctionPresetChanged -= UpdateFunctionPresetSelection;
            GlobalData.Inst.ActiveFunctionPresetChanged += UpdateFunctionPresetSelection;
            GlobalData.Inst.GoEar_AudioStateChanged -= UpdateGoEarAudioAction;
            GlobalData.Inst.GoEar_AudioStateChanged += UpdateGoEarAudioAction;
            GlobalData.Inst.GoEar_InStateChanged -= UpdateGoEarInAction;
            GlobalData.Inst.GoEar_InStateChanged += UpdateGoEarInAction;
            GlobalData.Inst.AutoMuteStateChanged -= UpdateAutoMuteAction;
            GlobalData.Inst.AutoMuteStateChanged += UpdateAutoMuteAction;
            UpdateAudioPresetSelection(GlobalData.Inst.ActiveAudioPreset);
            UpdateFunctionPresetSelection(GlobalData.Inst.ActiveFunctionPreset);
            UpdateGoEarAudioAction(GlobalData.Inst.GetGoEar_Audio());
            UpdateGoEarInAction(GlobalData.Inst.GetGoEar_In());
            UpdateAutoMuteAction(GlobalData.Inst.AutoMute);
        }

        private ObservableCollection<string> audioPresetNames = new ObservableCollection<string>();
        private List<FunctionPresetData> functionPresetOptions = new();
        private ObservableCollection<string> closeBehaviorOptions = new ObservableCollection<string>();

        private void UpdateAudioPresetSelection(AudioPresetData presetData)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateAudioPresetSelection(presetData));
                return;
            }

            AudioPresetComboBox.IsSyncing = true;
            audioPresetNames.Clear();
            audioPresetNames.Add(LanguageManager.Inst.GetString("无"));
            foreach (string name in AudioPresetDataTool.GetAllAudioPresetName())
                audioPresetNames.Add(name);

            int presetIndex = 0;
            if (presetData != null)
            {
                int nameIndex = audioPresetNames.IndexOf(presetData.Config.Name);
                if (nameIndex >= 0)
                    presetIndex = nameIndex;
            }

            AudioPresetComboBox.SelectedIndex = presetIndex;
            AudioPresetComboBox.IsSyncing = false;
        }

        private void RefreshFunctionPresetOptions()
        {
            functionPresetOptions = FunctionPresetDataTool.GetAll();
            if (functionPresetOptions.Count == 0)
            {
                var defaultPreset = FunctionPresetDataTool.CreateDefault();
                if (defaultPreset != null)
                    functionPresetOptions.Add(defaultPreset);
            }

            FunctionPresetComboBox.IsSyncing = true;
            FunctionPresetComboBox.ItemsSource = functionPresetOptions;
            FunctionPresetComboBox.SelectedIndex = functionPresetOptions.FindIndex(item =>
                string.Equals(
                    item.Name,
                    GlobalData.Inst.ActiveFunctionPreset?.Name,
                    StringComparison.OrdinalIgnoreCase));
            FunctionPresetComboBox.IsSyncing = false;
        }

        private void UpdateFunctionPresetSelection(FunctionPresetData functionPreset)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateFunctionPresetSelection(functionPreset));
                return;
            }

            RefreshFunctionPresetOptions();
            RefreshPlayingKeySettings();
        }

        private void RefreshCloseBehaviorOptions()
        {
            closeBehaviorOptions.Clear();
            closeBehaviorOptions.Add(LanguageManager.Inst.GetString("最小化到托盘"));
            closeBehaviorOptions.Add(LanguageManager.Inst.GetString("直接关闭"));
        }


        private void UpdateLanguageAction(System.Globalization.CultureInfo arg1, LanguageManager.LanguageInfo arg2)
        {
            int themeIndex = StyleListBox.SelectedIndex;
            StyleListBox.IsSyncing = true;
            StyleListBox.ItemsSource = null;
            StyleListBox.ItemsSource = ThemeManager.ThemeList;
            StyleListBox.SelectedIndex = themeIndex;
            StyleListBox.IsSyncing = false;

            int index = AudioPresetComboBox.SelectedIndex;
            AudioPresetComboBox.IsSyncing = true;
            audioPresetNames[0] = LanguageManager.Inst.GetString("无");
            AudioPresetComboBox.SelectedIndex = index;
            AudioPresetComboBox.IsSyncing = false;
            RefreshFunctionPresetOptions();

            int closeBehaviorIndex = CloseBehaviorComboBox.SelectedIndex;
            CloseBehaviorComboBox.IsSyncing = true;
            RefreshCloseBehaviorOptions();
            CloseBehaviorComboBox.SelectedIndex = closeBehaviorIndex;
            CloseBehaviorComboBox.IsSyncing = false;

            LanguageComboBox.IsSyncing = true;
            LanguageComboBox.SelectedIndex = arg2.Index;
            LanguageComboBox.IsSyncing = false;


            RefreshPlayingKeySettings();
        }

        private enum SettingToggleHotkeyKind
        {
            EarAudio,
            EarIn,
            AutoMute
        }

        private void EarAudioHotkeyInputTextBox_GotFocus(
            object sender,
            RoutedEventArgs e)
        {
            StartSettingHotkeyRecording(SettingToggleHotkeyKind.EarAudio);
        }

        private void EarInHotkeyInputTextBox_GotFocus(
            object sender,
            RoutedEventArgs e)
        {
            StartSettingHotkeyRecording(SettingToggleHotkeyKind.EarIn);
        }

        private void AutoMuteHotkeyInputTextBox_GotFocus(
            object sender,
            RoutedEventArgs e)
        {
            StartSettingHotkeyRecording(SettingToggleHotkeyKind.AutoMute);
        }

        private void SettingHotkeyInputTextBox_LostFocus(
            object sender,
            RoutedEventArgs e)
        {
            HotkeyManager.Inst.StopRecording();
        }

        private void StartSettingHotkeyRecording(SettingToggleHotkeyKind kind)
        {
            HotkeyManager.Inst.StartRecording(
                hotkey => OnSettingHotkeyRecorded(kind, hotkey));
        }

        private void OnSettingHotkeyRecorded(
            SettingToggleHotkeyKind kind,
            HotkeyData newHotkey)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                HotkeyData hotkey = GetSettingToggleHotkey(kind);
                if (newHotkey == null)
                {
                    hotkey.Clear();
                }
                else
                {
                    hotkey.Modifiers = newHotkey.Modifiers;
                    hotkey.VkCode = newHotkey.VkCode;
                    hotkey.IsMouse = newHotkey.IsMouse;
                }

                TextBox input = GetSettingToggleHotkeyInput(kind);
                input.Text = hotkey.ToString();
                input.SelectionStart = input.Text.Length;
                Keyboard.Focus(null);
                GlobalData.Inst.Config.Save();
            });
        }

        private static HotkeyData GetSettingToggleHotkey(
            SettingToggleHotkeyKind kind)
        {
            Config config = GlobalData.Inst.Config;
            return kind switch
            {
                SettingToggleHotkeyKind.EarAudio => config.EarAudioToggleHotkey,
                SettingToggleHotkeyKind.EarIn => config.EarInToggleHotkey,
                SettingToggleHotkeyKind.AutoMute => config.AutoMuteToggleHotkey,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private TextBox GetSettingToggleHotkeyInput(SettingToggleHotkeyKind kind)
        {
            return kind switch
            {
                SettingToggleHotkeyKind.EarAudio => EarAudioHotkeyInputTextBox,
                SettingToggleHotkeyKind.EarIn => EarInHotkeyInputTextBox,
                SettingToggleHotkeyKind.AutoMute => AutoMuteHotkeyInputTextBox,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private void RefreshPlayingKeySettings()
        {
            var functionPreset = GlobalData.Inst.ActiveFunctionPreset;
            if (functionPreset == null)
                return;

            KeyboardKeyInputTextBox0.Text =
                functionPreset.BeforePlayingKey.HotkeyData.ToString();
            KeyboardKeyInputTextBox1.Text =
                functionPreset.AfterPlayingKey.HotkeyData.ToString();

            List<PlayAudioKeyDataKeyAction> beforeActions = new()
            {
                new(PlayAudioKeyData.KeyAction.按下),
                new(PlayAudioKeyData.KeyAction.单击),
            };
            List<PlayAudioKeyDataKeyAction> afterActions = new()
            {
                new(PlayAudioKeyData.KeyAction.抬起),
                new(PlayAudioKeyData.KeyAction.单击),
            };

            BeforePlayingComboBox.IsSyncing = true;
            BeforePlayingComboBox.ItemsSource = beforeActions;
            BeforePlayingComboBox.DisplayMemberPath = "Name";
            BeforePlayingComboBox.SelectedIndex = beforeActions.FindIndex(item =>
                item.keyAction == functionPreset.BeforePlayingKey.Action);
            BeforePlayingComboBox.IsSyncing = false;

            AfterPlayingComboBox.IsSyncing = true;
            AfterPlayingComboBox.ItemsSource = afterActions;
            AfterPlayingComboBox.DisplayMemberPath = "Name";
            AfterPlayingComboBox.SelectedIndex = afterActions.FindIndex(item =>
                item.keyAction == functionPreset.AfterPlayingKey.Action);
            AfterPlayingComboBox.IsSyncing = false;
        }

        private void UpdateThemeAction(ThemeManager.ThemeInfo themeInfo)
        {
            StyleListBox.IsSyncing = true;
            StyleListBox.SelectedIndex = themeInfo.Index;
            StyleListBox.IsSyncing = false;
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
                    var hotkeyData = GlobalData.Inst.ActiveFunctionPreset.BeforePlayingKey.HotkeyData;
                    hotkeyData.Modifiers = newHotkey.Modifiers;
                    hotkeyData.VkCode = newHotkey.VkCode;
                    hotkeyData.IsMouse = newHotkey.IsMouse;

                    KeyboardKeyInputTextBox0.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox0.SelectionStart = KeyboardKeyInputTextBox0.Text.Length;
                }
                Keyboard.Focus(null);
                GlobalData.Inst.SaveActiveFunctionPreset();
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
                    var hotkeyData = GlobalData.Inst.ActiveFunctionPreset.AfterPlayingKey.HotkeyData;
                    hotkeyData.Modifiers = newHotkey.Modifiers;
                    hotkeyData.VkCode = newHotkey.VkCode;
                    hotkeyData.IsMouse = newHotkey.IsMouse;

                    KeyboardKeyInputTextBox1.Text = hotkeyData.ToString();
                    KeyboardKeyInputTextBox1.SelectionStart = KeyboardKeyInputTextBox1.Text.Length;
                }
                Keyboard.Focus(null);
                GlobalData.Inst.SaveActiveFunctionPreset();
            });
        }

        private void ClearHotkey(int index)
        {
            HotkeyData hotkeyData = null;
            switch (index)
            {
                case 0:
                    hotkeyData = GlobalData.Inst.ActiveFunctionPreset.BeforePlayingKey.HotkeyData;
                    break;
                case 1:
                    hotkeyData = GlobalData.Inst.ActiveFunctionPreset.AfterPlayingKey.HotkeyData;
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

