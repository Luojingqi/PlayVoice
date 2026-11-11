using Microsoft.Win32;
using PlayVoice.Pages.Loading;
using PlayVoice.Diagnostics;
using PlayVoice.Pages.Workshop;
using PlayVoice.Resources.Language;
using Steamworks;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using static PlayVoice.Pages.Workshop.WorkshopPage;
namespace PlayVoice.Pages.Preset
{
    /// <summary>
    /// UploadPresetPage.xaml 的交互逻辑
    /// </summary>
    public partial class UploadPresetPage : Page, IProgress<float>
    {
        private ObservableCollection<string> presetNameArray = new ObservableCollection<string>();

        private CircularProgressBar loadingPage;
        private bool isUploading;
        private bool isReportingSteamUploadProgress;
        public UploadPresetPage()
        {
            InitializeComponent();
            Unloaded += WorkshopEditor_Unloaded;
            loadingPage = new();
            loadingPage.Visibility = Visibility.Hidden;
            LoadingPageFrame.Content = loadingPage;

            TabListBox.ItemsSource = new TabItem<TabType>[]
                {
                    new TabItem<TabType>(TabType.音乐),
                    new TabItem<TabType>(TabType.音效),
                    new TabItem<TabType>(TabType.语音),
                };
            TabListBox.DisplayMemberPath = "Name";
            EditTagsListBox.ItemsSource = TabListBox.ItemsSource;
            EditTagsListBox.DisplayMemberPath = "Name";
            EditTagsListBox.OnSelectionChanged += (_, _) => UpdateEditSummary();

            ImageButton.SelectionChanged += ImageButton_SelectionChanged;
            ImageCloseButton.Click += ImageCloseButton_Click;

            ConfirmButton.SelectionChanged += ConfirmButton_SelectionChanged;

            ImageButton.Visibility = Visibility.Visible;
            BgImage.Visibility = Visibility.Collapsed;
            ImageCloseButton.Visibility = Visibility.Collapsed;

            TabListBox.OnSelectionChanged += TabListBox_OnSelectionChanged;

            VisibleComboBox.ItemsSource = new TabItem<VisibleType>[]
                {
                    new TabItem<VisibleType>(VisibleType.仅自己),
                    new TabItem<VisibleType>(VisibleType.仅好友),
                    new TabItem<VisibleType>(VisibleType.所有人),
                };
            VisibleComboBox.DisplayMemberPath = "Name";
            VisibleComboBox.SelectedIndex = 2;
        }

        public void Open(int index)
        {
            presetNameArray.Clear();
            var tempArray = AudioPresetDataTool.GetAllAudioPresetName();
            presetNameArray.Add(LanguageManager.Inst.GetString("无"));
            int presetIndex = 0;
            for (int i = 0; i < tempArray.Length; i++)
            {
                presetNameArray.Add(tempArray[i]);
                if (GlobalData.Inst.ActiveAudioPreset != null
                    && tempArray[i] == GlobalData.Inst.ActiveAudioPreset.Config.Name)
                    presetIndex = i + 1;
            }
            PresetComboBox.ItemsSource = presetNameArray;
            PresetComboBox.IsSyncing = true;
            if (index == -1)
                PresetComboBox.SelectedIndex = presetIndex;
            else
                PresetComboBox.SelectedIndex = index;
            PresetComboBox.IsSyncing = false;
        }

        private HashSet<TabItem<TabType>> tabSelectionSet = new();
        private void TabListBox_OnSelectionChanged(object arg1, object arg2)
        {
            if (arg1 != null) tabSelectionSet.Remove((TabItem<TabType>)arg1);
            if (arg2 != null) tabSelectionSet.Add((TabItem<TabType>)arg2);
        }

        private async void ConfirmButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfirmButton.SelectedIndex == -1 || isUploading) return;

            if (SteamClient.IsValid == false)
            {
                if (MainWindow.Inst.SteamInit() == false)
                {
                    goto end;
                }
            }

            string presetName = presetNameArray[PresetComboBox.SelectedIndex];
            if (string.IsNullOrEmpty(presetName) || presetName == LanguageManager.Inst.GetString("无"))
            {
                MainWindow.Inst.AddNotification(
                    () => LanguageManager.Inst.GetString("通知"),
                    () => LanguageManager.Inst.GetString("没有选择上传的音频预设"),
                    LabelStatus.Error, 4);
                goto end;
            }

            if (tabSelectionSet.Count == 0)
            {
                MainWindow.Inst.AddNotification(
                    () => LanguageManager.Inst.GetString("通知"),
                    () => LanguageManager.Inst.GetString("至少选择一个标签"),
                    LabelStatus.Error, 4);
                goto end;
            }

            string resourceName = ResourceNameInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(resourceName))
            {
                MainWindow.Inst.AddNotification(
                    () => LanguageManager.Inst.GetString("通知"),
                    () => LanguageManager.Inst.GetString("没有输入资源名称"),
                    LabelStatus.Error, 4);
                goto end;
            }

            var audioPresetConfig = AudioPresetDataTool.FindAudioPresetConfig(presetName);
            if (audioPresetConfig == null)
                goto end;

            if (!GlobalData.Inst.Config.AcceptedUserGeneratedContentAgreement)
            {
                var agreementWindow = new UserGeneratedContentAgreementWindow
                {
                    Owner = MainWindow.Inst
                };
                if (agreementWindow.ShowDialog() != true)
                    goto end;

                GlobalData.Inst.Config.AcceptedUserGeneratedContentAgreement = true;
                GlobalData.Inst.Config.Save();
            }

            isUploading = true;
            MainWindow.Inst.SetBodyInteractionBlocked(true);
            string tempPresetPath = null;
            try
            {
                loadingPage.Visibility = Visibility.Visible;
                loadingPage.SetProgress(0, 0);

                string resourceDescription = ResourceDescriptionInputTextBox.Text;
                MainWindow.Inst.AddNotification(
                       () => LanguageManager.Inst.GetString("通知"),
                       () => LanguageManager.Inst.GetString("正在复制文件"),
                       LabelStatus.Warning, 3.3f);

                string presetPath = Path.Combine(AudioPresetDataTool.BasePath, audioPresetConfig.Name);
                string tempRootPath = PresetStorage.TempPath;
                tempPresetPath = Path.Combine(tempRootPath, audioPresetConfig.Name);
                string extendedExplanationPath = PresetStorage.ExtendedExplanationPath;
                Directory.CreateDirectory(extendedExplanationPath);
                Report(0.38f);
                await JsonTool.CopyDirectoryReplaceTrueAsync(presetPath, tempPresetPath);
                Report(0.66f);
                string tempExtendedExplanationPath = Path.Combine(tempPresetPath, "ExtendedExplanation");
                Directory.CreateDirectory(tempExtendedExplanationPath);
                await JsonTool.CopyDirectoryReplaceTrueAsync(extendedExplanationPath, tempExtendedExplanationPath);
                long extendedExplanationTotalSize = PresetStorage.GetDirectorySize(tempExtendedExplanationPath);
                Report(0.82f);

                var uploadPresetData = await AudioPresetDataTool.LoadAudioPresetDataFromPath(
                    tempRootPath,
                    audioPresetConfig.Name);
                if (uploadPresetData == null)
                    throw new InvalidOperationException(LanguageManager.Inst.GetString("物品加载失败"));

                ResourceDataConfig.Metadata metaData;
                try
                {
                    metaData = ResourceDataConfig.Metadata.Create(uploadPresetData, tempPresetPath);
                }
                finally
                {
                    uploadPresetData.Dispose();
                }

                metaData.ExtendedExplanationTotalSize = extendedExplanationTotalSize;
                File.Delete(Path.Combine(tempPresetPath, AudioPresetDataTool.ConfigFileName));

                var workshopDescription = new WorkshopDescriptionModel
                {
                    UserDescription = resourceDescription,
                    AudioNames = metaData.ItemList.Select(item => item.FileName).ToArray()
                };
                string workshopDescriptionJson = JsonTool.ToJson(
                    workshopDescription,
                    writeIndented: true);

                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbnail.png");

                var resourceDataConfig = new ResourceDataConfig
                {
                    Title = resourceName,
                    Description = resourceDescription,
                    Data = metaData,
                    ThumbnailFormat = Path.GetExtension(imagePath)
                };
                if (!JsonTool.SaveJson(Path.Combine(tempPresetPath, "ResourceConfig.json"), resourceDataConfig))
                    throw new IOException(LanguageManager.Inst.GetString("上传Steam失败"));
                File.Copy(imagePath, Path.Combine(tempPresetPath, $"Thumbnail{resourceDataConfig.ThumbnailFormat}"));


                MainWindow.Inst.AddNotification(
                      () => LanguageManager.Inst.GetString("通知"),
                      () => LanguageManager.Inst.GetString("开始上传"),
                      LabelStatus.Warning, 9f);

                var editor = Editor.NewCommunityFile
                    .WithTitle(resourceName)
                    .WithDescription(workshopDescriptionJson)
                    .InLanguage(GetWorkshopUploadLanguage());
                foreach (var tab in tabSelectionSet)
                    editor = editor.WithTag(WorkshopPage.TabTypeToSteam(tab.Type));
                editor = editor.WithContent(tempPresetPath);
                if (File.Exists(imagePath))
                    editor = editor.WithPreviewFile(imagePath);

                editor = editor.WithMetaData(JsonTool.ToJson(metaData, writeIndented: false));
                switch (VisibleComboBox.SelectedIndex)
                {
                    case 0:
                        editor = editor.WithPrivateVisibility();
                        break;
                    case 1:
                        editor = editor.WithFriendsOnlyVisibility();
                        break;
                    case 2:
                        editor = editor.WithPublicVisibility();
                        break;
                }
                isReportingSteamUploadProgress = true;
                var result = await editor.SubmitAsync(this);
                isReportingSteamUploadProgress = false;

                if (result.Success)
                {
                    try
                    {
                        PresetStorage.ArchiveExtendedExplanation();
                        Report(1f);
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => LanguageManager.Inst.GetString("已上传到Steam"),
                            LabelStatus.Success, 4f);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error(
                            ex,
                            "创意工坊上传成功后归档扩展说明失败。",
                            "Workshop.Upload");
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"{LanguageManager.Inst.GetString("扩展说明归档失败")} : {ex.Message}",
                            LabelStatus.Error, 6f);
                    }
                }
                else
                {
                    MainWindow.Inst.AddNotification(
                        () => LanguageManager.Inst.GetString("通知"),
                        () => LanguageManager.Inst.GetString("上传Steam失败") + $" : {result.Result}",
                        LabelStatus.Error, 4f);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "上传 Steam 创意工坊失败。", "Workshop.Upload");
                MainWindow.Inst.AddNotification(
                    () => LanguageManager.Inst.GetString("通知"),
                    () => $"{LanguageManager.Inst.GetString("上传Steam失败")} : {ex.Message}",
                    LabelStatus.Error, 6f);
            }
            finally
            {
                isReportingSteamUploadProgress = false;
                if (!string.IsNullOrEmpty(tempPresetPath) && Directory.Exists(tempPresetPath))
                {
                    try
                    {
                        Directory.Delete(tempPresetPath, true);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warning(
                            ex,
                            $"清理创意工坊上传临时目录失败：{tempPresetPath}",
                            "Workshop.UploadCleanup");
                    }
                }
                loadingPage.Visibility = Visibility.Hidden;
                MainWindow.Inst.SetBodyInteractionBlocked(false);
                isUploading = false;
            }
        end:
            ConfirmButton.SelectedIndex = -1;
        }

        private static string GetWorkshopUploadLanguage()
        {
            string steamLanguage = SteamApps.GameLanguage;
            if (!string.IsNullOrWhiteSpace(steamLanguage))
                return steamLanguage;

            string clientCulture = LanguageManager.Inst.CurrentCulture.Name;
            return SteamWorkshopLanguage.FromClientCulture(clientCulture);
        }

        private void ImageCloseButton_Click(object sender, RoutedEventArgs e)
        {
            imagePath = null;
            BgImage.Source = null;
            ImageButton.Visibility = Visibility.Visible;
            BgImage.Visibility = Visibility.Collapsed;
            ImageCloseButton.Visibility = Visibility.Collapsed;
        }
        private string imagePath;
        private void ImageButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageButton.SelectedIndex == -1) return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = LanguageManager.Inst.GetString("选择预览图");
            openFileDialog.Filter = $"{LanguageManager.Inst.GetString("图片文件")} (*.jpg;*.png;*gif)|*.jpg;*.png;*.gif";
            openFileDialog.FilterIndex = 1;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            if (true == openFileDialog.ShowDialog())
            {
                FileInfo fileInfo = new FileInfo(openFileDialog.FileName);
                long maxSize = GlobalData.Inst.Config.WorkshopPreviewMaxBytes;
                if (fileInfo.Length > maxSize)
                {
                    MainWindow.Inst.AddNotification(
                        () => LanguageManager.Inst.GetString("通知"),
                        () => LanguageManager.Inst.GetString("预览图要求"),
                        LabelStatus.Error);
                }
                else
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    int width = bitmap.PixelWidth;
                    int height = bitmap.PixelHeight;
                    if (Math.Abs(width - height) > GlobalData.Inst.Config.WorkshopPreviewSquareTolerance)
                    {
                        MainWindow.Inst.AddNotification(
                             () => LanguageManager.Inst.GetString("通知"),
                             () => LanguageManager.Inst.GetString("预览图要求"),
                             LabelStatus.Error);
                    }
                    else
                    {
                        BgImage.Source = bitmap;
                        ImageButton.Visibility = Visibility.Collapsed;
                        BgImage.Visibility = Visibility.Visible;
                        ImageCloseButton.Visibility = Visibility.Visible;
                        imagePath = openFileDialog.FileName;
                    }
                }
            }

            ImageButton.SelectedIndex = -1;
        }

        public void SetProgress(double progress, int durationMilliseconds = 300)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            var animation = new DoubleAnimation
            {
                To = progress,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMilliseconds)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            ProgressText.Text = $"{(int)(progress * 100)}%";
        }

        public void SetProgressInstant(double progress)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            ProgressScale.ScaleX = progress;
        }

        public void Report(float value)
        {
            double progress = isReportingSteamUploadProgress
                ? 0.82 + Math.Clamp(value, 0, 1) * 0.15
                : value;
            SetProgress(progress, 250);
            loadingPage.SetProgress(progress, 250);
            if (EditPanel.Visibility == Visibility.Visible)
                EditProgress.SetProgress(progress, 250);
        }
    }
}
