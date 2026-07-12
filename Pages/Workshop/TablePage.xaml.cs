using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PlayVoice.Pages.Workshop;

public partial class TablePage : UserControl
{
    private UniformGrid _dynamicGrid;

    private const double ItemMinWidth = 150;
    private const double ItemMaxWidth = 150;
    private const int ItemMinColumn = 1;

    private ObservableCollection<TablePageItem> TableItemList = new();

    public static HashSet<TablePageItem> NowDownloadingItemSet = new();

    public TablePage()
    {
        InitializeComponent();

        ItemsControl.ItemsSource = TableItemList;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    }

    static TablePage()
    {
        CheckDownload();
    }

    private static List<TablePageItem> removeTableItemList = new();
    private static async void CheckDownload()
    {
        while (true)
        {
            await Task.Delay(500);
            foreach (var tableItem in NowDownloadingItemSet)
            {
                if (tableItem.Item.HasValue)
                {
                    tableItem.LoadingBar.SetProgress(tableItem.Item.Value.DownloadAmount);

                    var reItem = await SteamUGC.QueryFileAsync(tableItem.Item.Value.Id);
                    if (reItem.HasValue && reItem.Value.IsInstalled == false) continue;
                    else
                    {
                        removeTableItemList.Add(tableItem);
                    }
                }
            }
            foreach(var tableItem in removeTableItemList)
            {
                NowDownloadingItemSet.Remove(tableItem);
                await WorkshopPage.Inst.TablePage.ReLoadItem(tableItem);
            }
            removeTableItemList.Clear();
        }
    }


    #region 拖拽宽度，子元素自适应
    private void CompositionTarget_Rendering(object sender, EventArgs e) => RecalculateLayout();
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
        if (_dynamicGrid.Columns != columns)
            _dynamicGrid.Columns = columns;
    }
    #endregion



    public async Task SwitchPage(WorkshopPage.PageType pageType, int nowPage = 1)
    {
        Clear();
        WorkshopPage.Inst.PageNumberSelection.Close();
        switch (pageType)
        {
            case WorkshopPage.PageType.我的订阅:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        if (SteamClient.IsValid == false) return;
                    //只有此，需要再次判断，如果未登录，但是steam已经启动，可以尝试获取缓存
                    var query = Query.Items.WhereUserSubscribed().AllowCachedResponse(10);
                    query = query.WithMetadata(true);
                    ResultPage? page = await query.GetPageAsync(nowPage);
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        WorkshopPage.Inst.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                            if (item.IsInstalled)
                            {
                                var tableItem = AddItem(item, item.Directory);
                                tableItem.OnClick += WorkshopPage.Inst.OnClickTableItem_OpenLocalDetailPage;
                            }
                            else
                            {
                                var tableItem = AddItem(item, null);
                                tableItem.OnClick += WorkshopPage.Inst.OnClickTableItem_OpenDetailPage;
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
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        WorkshopPage.Inst.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += WorkshopPage.Inst.OnClickTableItem_OpenDetailPage;
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
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        WorkshopPage.Inst.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += WorkshopPage.Inst.OnClickTableItem_OpenDetailPage;
                        }
                    }
                    break;
                }
            case WorkshopPage.PageType.创意工坊:
                {
                    if (SteamClient.IsValid == false && MainWindow.Inst.SteamInit() == false)
                        return;
                    var query = Query.All.AllowCachedResponse(10);
                    if (WorkshopPage.Inst.SearchText != null && !string.IsNullOrEmpty(WorkshopPage.Inst.SearchText.Trim()))
                    {
                        query = query.WhereSearchText(WorkshopPage.Inst.SearchText.Trim())
                            .RankedByTextSearch();
                    }
                    else
                    {
                        switch ((WorkshopPage.RankedType)WorkshopPage.Inst.RankedComboBox.SelectedIndex)
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

                    foreach (var item in WorkshopPage.Inst.BottomButtonListBox.SelectedItems)
                        query = query.WithTag(WorkshopPage.TabTypeToSteam(((WorkshopPage.TabItem<WorkshopPage.TabType>)item).Type));
                    query = query.MatchAnyTag();
                    query = query.WithMetadata(true);

                    var page = await query.GetPageAsync(nowPage);
                    if (page.HasValue && page.Value.ResultCount > 0)
                    {
                        int totalItems = page.Value.TotalCount;
                        int totalPages = (int)Math.Ceiling((double)totalItems / 50);
                        WorkshopPage.Inst.PageNumberSelection.Open(nowPage, totalPages);
                        foreach (Item item in page.Value.Entries)
                        {
                            var tableItem = AddItem(item, null);
                            tableItem.OnClick += WorkshopPage.Inst.OnClickTableItem_OpenDetailPage;
                        }
                    }
                }
                break;
        }
    }

    public TablePageItem AddItem(Item? item, string localItemPath)
    {
        var tableItem = new TablePageItem(item, localItemPath);
        TableItemList.Add(tableItem);

        return tableItem;
    }


    public async Task<TablePageItem> ReLoadItem(TablePageItem tableItem)
    {

        for (int i = 0; i < TableItemList.Count; i++)
        {
            if (tableItem == TableItemList[i])
            {
                if (tableItem.Item.HasValue)
                {
                    var item = await SteamUGC.QueryFileAsync(tableItem.Item.Value.Id);
                    if (item.HasValue)
                    {
                        var newTableItem = new TablePageItem(tableItem.Item.Value, null);
                        TableItemList[i] = newTableItem;
                        return newTableItem;
                    }
                }
                else
                {
                    TableItemList.RemoveAt(i);
                    return null;
                }
            }
        }
        return null;
    }



    public void Clear()
    {
        TableItemList.Clear();
        removeTableItemList.Clear();
        NowDownloadingItemSet.Clear();
    }
}