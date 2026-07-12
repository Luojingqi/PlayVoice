using PlayVoice.Resources.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PlayVoice.Pages.Setting
{
    /// <summary>
    /// VirtualSoundCardInstallationPage.xaml 的交互逻辑
    /// </summary>
    public partial class VirtualSoundCardInstallationPage : Page
    {
        public VirtualSoundCardInstallationPage()
        {
            InitializeComponent();

            ButtonGroupListBox.SelectionChanged += ButtonGroupListBox_SelectionChanged;
        }
        private readonly Stopwatch _clickStopwatch = new Stopwatch();
        private const int ThrottleMilliseconds = 500;
        private bool _isProcessing = false;
        private async void ButtonGroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ButtonGroupListBox.SelectedIndex == -1) return;
            if (_isProcessing || _clickStopwatch.IsRunning && _clickStopwatch.ElapsedMilliseconds < ThrottleMilliseconds)
            {
                MainWindow.Inst.AddNotification(
                           () => LanguageManager.Inst.GetString("通知"),
                           () => $"{LanguageManager.Inst.GetString("请勿重复点击")}",
                           LabelStatus.Warning);
                return;
            }
            _clickStopwatch.Restart();
            switch (ButtonGroupListBox.SelectedIndex)
            {
                case 0:
                    if (EquipmentLoder.IsVBCableInstalled() == true)
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("已安装")}",
                            LabelStatus.Error);
                    else
                    {
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("正在安装")}\n{LanguageManager.Inst.GetString("请勿重复点击")}",
                            LabelStatus.Warning);
                        _isProcessing = true;
                        int code = await EquipmentLoder.InstallSilentAsync();
                        _isProcessing = false;
                        if (code == 0)
                            MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("安装结束")}",
                            LabelStatus.Success);
                        else
                            MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("安装未完成")}",
                            LabelStatus.Error);
                    }
                    break;
                case 1:
                    if (EquipmentLoder.IsVBCableInstalled() == false)
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("未安装")}",
                            LabelStatus.Error);
                    else
                    {
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("正在卸载")}\n{LanguageManager.Inst.GetString("请勿重复点击")}",
                            LabelStatus.Warning);
                        _isProcessing = true;
                        int code = await EquipmentLoder.UninstallSilentAsync();
                        _isProcessing = false;
                        if (code == 0)
                            MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("卸载结束")}",
                            LabelStatus.Success);
                        else
                            MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("卸载未完成")}",
                            LabelStatus.Error);
                    }
                    break;
                case 2:
                    bool isInstalled = EquipmentLoder.IsVBCableInstalled();
                    if (isInstalled)
                        MainWindow.Inst.AddNotification(
                                () => LanguageManager.Inst.GetString("通知"),
                                () => $"VB-CABLE {LanguageManager.Inst.GetString("已安装")}",
                                LabelStatus.Success);
                    else
                        MainWindow.Inst.AddNotification(
                            () => LanguageManager.Inst.GetString("通知"),
                            () => $"VB-CABLE {LanguageManager.Inst.GetString("未安装")}",
                            LabelStatus.Error);
                    break;
            }
            ButtonGroupListBox.SelectedIndex = -1;
        }
    }
}
