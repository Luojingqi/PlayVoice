using Steamworks;
using Steamworks.Data;
using PlayVoice.Diagnostics;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace PlayVoice.Pages.Workshop;

public partial class TablePage : UserControl
{
    private UniformGrid _dynamicGrid;

    private const double ItemMinWidth = 150;
    private const double ItemMaxWidth = 150;
    private const int ItemMinColumn = 1;

    private ObservableCollection<TablePageItem> TableItemList = new();
    private readonly DispatcherTimer _downloadProgressTimer;
    private readonly HashSet<PublishedFileId> _refreshingItemIds = new();
    private int _loadGeneration;
    private WorkshopPage _owner;

    public TablePage()
    {
        InitializeComponent();

        ItemsControl.ItemsSource = TableItemList;
        _downloadProgressTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _downloadProgressTimer.Tick += DownloadProgressTimer_Tick;
        Loaded += TablePage_Loaded;
        Unloaded += TablePage_Unloaded;
        SizeChanged += TablePage_SizeChanged;
    }

    public void SetOwner(WorkshopPage owner) => _owner = owner;

    private void TablePage_Loaded(object sender, RoutedEventArgs e) => _downloadProgressTimer.Start();

    private void TablePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _downloadProgressTimer.Stop();
        _loadGeneration++;
        _refreshingItemIds.Clear();
        TableItemList.Clear();
    }

    private void DownloadProgressTimer_Tick(object sender, EventArgs e)
    {
        foreach (var tableItem in TableItemList.ToArray())
        {
            if (!tableItem.Item.HasValue || !tableItem.UpdateDownloadProgress()) continue;

            var itemId = tableItem.Item.Value.Id;
            if (_refreshingItemIds.Add(itemId))
                RefreshCompletedItemAsync(tableItem, itemId)
                    .Forget("Workshop.Table.DownloadComplete");
        }
    }

    private async Task RefreshCompletedItemAsync(TablePageItem tableItem, PublishedFileId itemId)
    {
        try
        {
            await ReLoadItem(tableItem);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "刷新创意工坊物品失败。", "Workshop.Table");
        }
        finally
        {
            _refreshingItemIds.Remove(itemId);
        }
    }


    #region 拖拽宽度，子元素自适应
    private void TablePage_SizeChanged(object sender, SizeChangedEventArgs e) => RecalculateLayout();
    private void DynamicGrid_Loaded(object sender, RoutedEventArgs e) => _dynamicGrid = sender as UniformGrid;
    private void RecalculateLayout()
    {
        double availableWidth = ItemsControl.ActualWidth;
        if (availableWidth <= 0) return;

        int columns = (int)Math.Floor(availableWidth / ItemMaxWidth);
        if (columns < ItemMinColumn) columns = ItemMinColumn;

        while (availableWidth / columns > ItemMaxWidth)
        {
            columns++;
        }
        if (_dynamicGrid != null && _dynamicGrid.Columns != columns)
            _dynamicGrid.Columns = columns;
    }
    #endregion



    public async Task SwitchPage(WorkshopPage.PageType pageType, int nowPage = 1)
    {
        int generation = ++_loadGeneration;
        TableItemList.Clear();
        _refreshingItemIds.Clear();
        _owner.PageNumberSelection.Close();
        switch (pageType)
        {
            case WorkshopPage.PageType.我的订阅:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        if (SteamClient.IsValid == false) return;
                    //只有此，需要再次判断，如果未登录，但是steam已经启动，可以尝试获取缓存
                    var query = Query.Items.WhereUserSubscribed();
                    query = query.WithMetadata(true);
                    ResultPage? page = await query.GetPageAsync(nowPage);
                    if (!IsCurrentLoad(generation)) return;
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        _owner.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                            if (item.IsInstalled)
                            {
                                var tableItem = AddItem(item, item.Directory);
                                tableItem.OnClick += _owner.OnClickTableItem_OpenLocalDetailPage;
                            }
                            else
                            {
                                var tableItem = AddItem(item, null);
                                tableItem.OnClick += _owner.OnClickTableItem_OpenDetailPage;
                            }
                       
                    }
                }
                break;
            case WorkshopPage.PageType.我的创作:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        return;
                    var query = Query.All.WhereUserPublished().AllowCachedResponse(10);
                    query = query.WithMetadata(true);
                    var page = await query.GetPageAsync(nowPage);
                    if (!IsCurrentLoad(generation)) return;
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        _owner.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += _owner.OnClickTableItem_OpenDetailPage;
                        }
                    }
                    break;
                }
            case WorkshopPage.PageType.好友创作:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        return;
                    var query = Query.Screenshots.CreatedByFriends().AllowCachedResponse(10);
                    query = query.WithMetadata(true);
                    var page = await query.GetPageAsync(nowPage);
                    if (!IsCurrentLoad(generation)) return;
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        _owner.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += _owner.OnClickTableItem_OpenDetailPage;
                        }
                    }
                    break;
                }
            case WorkshopPage.PageType.创意工坊:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        return;
                    var query = Query.All.AllowCachedResponse(10);
                    if (_owner.SearchText != null && !string.IsNullOrEmpty(_owner.SearchText.Trim()))
                    {
                        query = query.WhereSearchText(_owner.SearchText.Trim())
                            .RankedByTextSearch();
                    }
                    else
                    {
                        switch ((WorkshopPage.RankedType)_owner.RankedComboBox.SelectedIndex)
                        {
                            case WorkshopPage.RankedType.热度:
                                query = query.RankedByTrend();
                                break;
                            case WorkshopPage.RankedType.评分:
                                query = query.RankedByVote();
                                break;
                            case WorkshopPage.RankedType.最新:
                                query = query.RankedByPublicationDate();
                                break;
                            case WorkshopPage.RankedType.订阅:
                                query = query.RankedByTotalUniqueSubscriptions();
                                break;
                        }
                    }

                    foreach (var item in _owner.BottomButtonListBox.SelectedItems)
                        query = query.WithTag(WorkshopPage.TabTypeToSteam(((WorkshopPage.TabItem<WorkshopPage.TabType>)item).Type));
                    query = query.MatchAnyTag();
                    query = query.WithMetadata(true);

                    var page = await query.GetPageAsync(nowPage);
                    if (!IsCurrentLoad(generation)) return;
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        _owner.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += _owner.OnClickTableItem_OpenDetailPage;
                        }
                    }
                }
                break;
        }
    }

    private bool IsCurrentLoad(int generation) =>
        generation == _loadGeneration && IsLoaded && ReferenceEquals(_owner.TablePage, this);

    public TablePageItem AddItem(Item? item, string localItemPath)
    {
        var tableItem = new TablePageItem(item, localItemPath);
        TableItemList.Add(tableItem);

        return tableItem;
    }


    public async Task<TablePageItem> ReLoadItem(TablePageItem tableItem)
    {
        if (!tableItem.Item.HasValue)
        {
            TableItemList.Remove(tableItem);
            return null;
        }

        var item = await SteamUGC.QueryFileAsync(tableItem.Item.Value.Id);
        if (!item.HasValue) return null;

        int index = TableItemList.IndexOf(tableItem);
        if (index < 0) return null;

        var newTableItem = new TablePageItem(item.Value, item.Value.IsInstalled ? item.Value.Directory : null);
        newTableItem.OnClick += item.Value.IsInstalled
            ? _owner.OnClickTableItem_OpenLocalDetailPage
            : _owner.OnClickTableItem_OpenDetailPage;

        // IndexOf 后集合仍只可能在 UI 线程变化；当前 continuation 也回到 UI 线程。
        if (index < TableItemList.Count && ReferenceEquals(TableItemList[index], tableItem))
        {
            TableItemList[index] = newTableItem;
            return newTableItem;
        }

        return null;
    }



    public void Clear()
    {
        _loadGeneration++;
        TableItemList.Clear();
        _refreshingItemIds.Clear();
    }
}
