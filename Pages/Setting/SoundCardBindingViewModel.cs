using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;
using PlayVoice.Diagnostics;
using PlayVoice.Pages.DropDownList;
using PlayVoice.Resources.Language;
using System.Globalization;

namespace PlayVoice.Pages.Setting;

public partial class SoundCardBindingPageViewModel : ObservableObject
{

    public class ComboBoxItemModel
    {
        public string Name { get; set; }
        public string ID { get; set; }
    }

    /// <summary>
    /// 解决更新数据后界面不刷新的bug，我们使用两个集合交替显示
    /// </summary>
    public class ItemSource
    {
        private Dictionary<string, ComboBoxItemModel> itemsSource0 = new();
        private Dictionary<string, ComboBoxItemModel> itemsSource1 = new();
        private int index = 0;
        public Dictionary<string, ComboBoxItemModel> GetNextItemsSource()
        {
            if (index == 0)
            {
                index = 1;
                return itemsSource1;
            }
            else
            {
                index = 0;
                return itemsSource0;
            }
        }
        public Dictionary<string, ComboBoxItemModel> GetNowItemsSource()
        {
            if (index == 0)
                return itemsSource0;
            else
                return itemsSource1;
        }
    }

    public class DropDownListModel
    {
        public ItemSource ItemSource;
        public DropDownList.DropDownList DropDownList;

        private SoundCardBindingPage soundCardBinding;
        private Func<MMDevice, bool> setDeviceAction;
        private Func<string, MMDevice> getDeviceAction;
        private Func<MMDevice> getCurrentDeviceAction;

        public DropDownListModel(
            SoundCardBindingPage soundCardBinding,
            DropDownList.DropDownList dropDownList,
            Dictionary<string, string> initDeviceDic,
            ComboBoxItemModel nullItem,
            Func<MMDevice, bool> setDeviceAction,
            Func<string, MMDevice> getDeviceAction,
            Func<MMDevice> getCurrentDeviceAction)
        {
            this.soundCardBinding = soundCardBinding;
            DropDownList = dropDownList;
            ItemSource = new();
            this.setDeviceAction = setDeviceAction;
            this.getDeviceAction = getDeviceAction;
            this.getCurrentDeviceAction = getCurrentDeviceAction;
            var equipment = GlobalData.Inst.Equipment;
            DropDownList.DisplayMemberPath = "Name";
            var itemsSource = ItemSource.GetNowItemsSource();
            itemsSource.Add(nullItem.ID, nullItem);
            foreach (var device in initDeviceDic)
                itemsSource.Add(device.Key, new() { ID = device.Key, Name = device.Value });
            dropDownList.ItemsSource = itemsSource.Values;
            dropDownList.SelectedIndex = 0;
            dropDownList.OnSelectionChanged += SelectionChanged;
        }

        public void OnDeviceRemove(string id, string name)
        {
            var nowItemsSource = ItemSource.GetNowItemsSource();
            if (nowItemsSource.TryGetValue(id, out var removeItem))
            {
                var selectedItem = DropDownList.SelectedItem as ComboBoxItemModel;
                nowItemsSource.Remove(id);
                var nextItemsSource = ItemSource.GetNextItemsSource();
                nextItemsSource.Clear();

                bool success = false;
                int index = 0;
                foreach (var kv in nowItemsSource)
                {
                    if (success == false)
                    {
                        if (selectedItem != null && kv.Value.ID == selectedItem.ID)
                            success = true;
                        else index++;
                    }
                    nextItemsSource.Add(kv.Key, kv.Value);
                }
                if (success == false)
                    index = 0;
                DropDownList.IsSyncing = true;
                DropDownList.ItemsSource = nextItemsSource.Values;
                DropDownList.SelectedIndex = index;
                DropDownList.IsSyncing = false;
                nowItemsSource.Clear();
            }
        }
        public void OnDeviceAdd(string id, string name)
        {
            var nowItemsSource = ItemSource.GetNowItemsSource();
            var nextItemsSource = ItemSource.GetNextItemsSource();
            nextItemsSource.Clear();
            foreach (var kv in nowItemsSource)
                nextItemsSource.Add(kv.Key, kv.Value);
            nextItemsSource.Add(id, new() { ID = id, Name = name });
            int selectedIndex = DropDownList.SelectedIndex;
            DropDownList.IsSyncing = true;
            DropDownList.ItemsSource = nextItemsSource.Values;
            DropDownList.SelectedIndex = selectedIndex;
            DropDownList.IsSyncing = false;
            nowItemsSource.Clear();
        }
        /// <summary>
        /// ui=>Data
        /// </summary>
        private void SelectionChanged(object arg1, object arg2)
        {
            var addeItem = (ComboBoxItemModel)arg2;
            if (addeItem == null) return;

            MMDevice device = null;
            try
            {
                if (!string.IsNullOrEmpty(addeItem.ID))
                {
                    device = getDeviceAction(addeItem.ID)
                        ?? throw new InvalidOperationException(
                            $"设备在绑定前已不可用：{addeItem.ID}");
                }

                bool accepted = setDeviceAction(device);
                device = null;
                if (!accepted)
                {
                    RestoreCurrentSelection();
                    ShowBindingFailureNotification();
                }
            }
            catch (Exception exception)
            {
                try
                {
                    device?.Dispose();
                }
                catch (Exception disposeException)
                {
                    AppLog.Warning(
                        disposeException,
                        "释放绑定失败的候选设备时出错。",
                        "SoundCardBinding.Selection");
                }

                AppLog.Error(
                    exception,
                    "选择音频设备失败，已恢复原绑定。",
                    "SoundCardBinding.Selection");
                RestoreCurrentSelection();
                ShowBindingFailureNotification();
            }
        }

        private void RestoreCurrentSelection()
        {
            try
            {
                OnDeviceChanged(null, getCurrentDeviceAction());
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    exception,
                    "恢复音频设备下拉框选择失败。",
                    "SoundCardBinding.Selection");
            }
        }

        private static void ShowBindingFailureNotification()
        {
            MainWindow.Inst.AddNotification(
                () => LanguageManager.Inst.GetString("通知"),
                () => LanguageManager.Inst.GetString("音频服务启动失败，请检查设备"),
                LabelStatus.Error,
                5);
        }
        /// <summary>
        /// Data=>ui
        /// </summary>
        public void OnDeviceChanged(MMDevice removeItem, MMDevice addItem)
        {
            DropDownList.IsSyncing = true;
            try
            {
                if (addItem == null)
                {
                    DropDownList.SelectedIndex = 0;
                    return;
                }

                string deviceID = addItem.ID;
                var nowItemsSource = ItemSource.GetNowItemsSource();
                if (nowItemsSource.ContainsKey(deviceID))
                {
                    DropDownList.SelectedIndex = nowItemsSource.Values
                        .TakeWhile(item => item.ID != deviceID)
                        .Count();
                    return;
                }

                var nextItemSource = ItemSource.GetNextItemsSource();
                nextItemSource.Clear();
                foreach (var kv in nowItemsSource)
                    nextItemSource.Add(kv.Key, kv.Value);
                nextItemSource.Add(
                    deviceID,
                    new ComboBoxItemModel
                    {
                        Name = addItem.FriendlyName,
                        ID = deviceID
                    });
                DropDownList.ItemsSource = nextItemSource.Values;
                DropDownList.SelectedIndex = nextItemSource.Count - 1;
                nowItemsSource.Clear();
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    exception,
                    "同步音频设备下拉框失败。",
                    "SoundCardBinding.DeviceChanged");
                DropDownList.SelectedIndex = 0;
            }
            finally
            {
                DropDownList.IsSyncing = false;
            }
        }

        public void UpdateLanguage(CultureInfo culture, LanguageManager.LanguageInfo languageInfo)
        {
            Update();
        }

        private void Update()
        {
            var nowItemsSource = ItemSource.GetNowItemsSource();
            var nextItemsSource = ItemSource.GetNextItemsSource();
            nextItemsSource.Clear();
            foreach (var kv in nowItemsSource)
                nextItemsSource.Add(kv.Key, kv.Value);
            int selectedIndex = DropDownList.SelectedIndex;
            DropDownList.IsSyncing = true;
            DropDownList.ItemsSource = nextItemsSource.Values;
            DropDownList.SelectedIndex = -1;
            DropDownList.SelectedIndex = selectedIndex;
            DropDownList.IsSyncing = false;
            nowItemsSource.Clear();
        }

        public void SetState(bool state)
        {
            if (state == false)
                DropDownList.LabelStatus = LabelStatus.Error;
            else
                DropDownList.LabelStatus = LabelStatus.Success;
        }
    }
}
