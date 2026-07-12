using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.CoreAudioApi;
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
        private Action<MMDevice> setDeviceAction;
        private Func<string, MMDevice> getDeviceAction;

        public DropDownListModel(
            SoundCardBindingPage soundCardBinding,
            DropDownList.DropDownList dropDownList,
            Dictionary<string, string> initDeviceDic,
            ComboBoxItemModel nullItem,
            Action<MMDevice> setDeviceAction,
            Func<string, MMDevice> getDeviceAction)
        {
            this.soundCardBinding = soundCardBinding;
            DropDownList = dropDownList;
            ItemSource = new();
            this.setDeviceAction = setDeviceAction;
            this.getDeviceAction = getDeviceAction;
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
                var selectedItem = (ComboBoxItemModel)DropDownList.SelectedItem;
                nowItemsSource.Remove(id);
                var nextItemsSource = ItemSource.GetNextItemsSource();

                bool success = false;
                int index = 0;
                foreach (var kv in nowItemsSource)
                {
                    if (success == false)
                    {
                        if (kv.Value.ID == selectedItem.ID)
                            success = true;
                        else index++;
                    }
                    nextItemsSource.Add(kv.Key, kv.Value);
                }
                if (success == false)
                    index = 0;
                DropDownList.IsSyncing = true;
                DropDownList.ItemsSource = nextItemsSource.Values;
                DropDownList.IsSyncing = false;
                DropDownList.SelectedIndex = index;
                nowItemsSource.Clear();
            }
        }
        public void OnDeviceAdd(string id, string name)
        {
            var nowItemsSource = ItemSource.GetNowItemsSource();
            var nextItemsSource = ItemSource.GetNextItemsSource();
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
            if (GlobalData.Inst.GetRun() == true)
            {
                MainWindow.Inst.AddNotification(
                    () => $"{LanguageManager.Inst.GetString("通知")}",
                    () => $"{LanguageManager.Inst.GetString("运行中禁止更改设备")}",
                    LabelStatus.Error);
                DropDownList.IsSyncing = true;
                var oldItem = ((ComboBoxItemModel)arg1);
                var nowItemsSource = ItemSource.GetNowItemsSource();
                int i = 0;
                foreach (var kv in nowItemsSource)
                    if (kv.Value == oldItem)
                    {
                        DropDownList.SelectedIndex = i;
                        break;
                    }
                    else i++;

                DropDownList.IsSyncing = false;
            }
            else if (!string.IsNullOrEmpty(addeItem.ID))
            {
                setDeviceAction.Invoke(getDeviceAction(addeItem.ID));
            }
            else
            {
                setDeviceAction.Invoke(null);
            }
        }
        /// <summary>
        /// Data=>ui
        /// </summary>
        public void OnDeviceChanged(MMDevice removeItem, MMDevice addItem)
        {
            DropDownList.IsSyncing = true;
            if (addItem == null)
                DropDownList.SelectedIndex = 0;
            else
            {
                var nowItemsSource = ItemSource.GetNowItemsSource();

                if (nowItemsSource.ContainsKey(addItem.ID))
                {
                    int index = 0;
                    foreach (var kv in nowItemsSource)
                        if (kv.Value.ID == addItem.ID)
                        {
                            DropDownList.SelectedIndex = index;
                            break;
                        }
                        else index++;
                }
                else
                {
                    var nextItemSource = ItemSource.GetNextItemsSource();
                    foreach (var kv in nowItemsSource)
                        nextItemSource.Add(kv.Key, kv.Value);
                    nextItemSource.Add(addItem.ID, new ComboBoxItemModel { Name = addItem.FriendlyName, ID = addItem.ID });
                    DropDownList.ItemsSource = nextItemSource.Values;
                    DropDownList.SelectedIndex = nextItemSource.Count - 1;
                    nowItemsSource.Clear();
                }
            }
            DropDownList.IsSyncing = false;
        }

        public void UpdateLanguage(CultureInfo culture, LanguageManager.LanguageInfo languageInfo)
        {
            Update();
        }

        private void Update()
        {
            var nowItemsSource = ItemSource.GetNowItemsSource();
            var nextItemsSource = ItemSource.GetNextItemsSource();
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