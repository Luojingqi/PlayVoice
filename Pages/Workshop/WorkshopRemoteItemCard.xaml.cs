using PlayVoice.Diagnostics;
using PlayVoice.Resources.Language;
using Steamworks;
using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PlayVoice.Pages.Workshop;

/// <summary>
/// 创意工坊公共浏览页中的单个远程物品卡片。
/// 负责加载和展示一个物品，并封装与该物品有关的 Steam 操作。
/// </summary>
public partial class WorkshopRemoteItemCard : UserControl
{
    private readonly ObservableCollection<WorkshopRemoteAudioItem> _audioItems = new();
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private Item _item;
    private bool _hasItem;
    private bool _isSubscribed;
    private bool? _userVote;
    private bool _isFavorite;

    public WorkshopRemoteItemCard()
    {
        InitializeComponent();
        AudioItemsControl.ItemsSource = _audioItems;
        Loaded += WorkshopRemoteItemCard_Loaded;
        Unloaded += WorkshopRemoteItemCard_Unloaded;
    }

    public static readonly DependencyProperty WorkshopItemProperty =
        DependencyProperty.Register(
            nameof(WorkshopItem),
            typeof(Item),
            typeof(WorkshopRemoteItemCard),
            new PropertyMetadata(default(Item), WorkshopItemChanged));

    public Item WorkshopItem
    {
        get => (Item)GetValue(WorkshopItemProperty);
        set => SetValue(WorkshopItemProperty, value);
    }

    private static void WorkshopItemChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var card = (WorkshopRemoteItemCard)dependencyObject;
        if (eventArgs.NewValue is not Item item)
        {
            card._hasItem = false;
            card.CancelCurrentLoad();
            return;
        }

        card._item = item;
        card._hasItem = true;
        if (card.IsLoaded)
            card.LoadItemAsync().Forget("Workshop.HorizontalCard.Reload");
    }

    private void WorkshopRemoteItemCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasItem)
            LoadItemAsync().Forget("Workshop.HorizontalCard.Load");
    }

    private void WorkshopRemoteItemCard_Unloaded(object sender, RoutedEventArgs e) =>
        CancelCurrentLoad();

    private async Task LoadItemAsync()
    {
        CancelCurrentLoad();
        ResetVisualState();

        int generation = _loadGeneration;
        _loadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _loadCancellation.Token;
        Item item = _item;

        ItemTitle.Text = item.Title;
        ItemTitle.ToolTip = item.Title;
        SteamScoreText.Text = $"★ {item.Score * 5:0.0}";
        _isSubscribed = item.IsSubscribed;
        UpdateSubscribeButtons();

        WorkshopRemoteItemContent content = WorkshopRemoteItemContent.Create(item);
        foreach (WorkshopRemoteAudioItem audioItem in content.AudioItems)
            _audioItems.Add(audioItem);

        AudioCountText.Text = content.AudioCount;
        ResourceSizeText.Text = content.TotalSize;
        if (!string.IsNullOrWhiteSpace(content.UserDescription))
        {
            ResourceDescriptionText.Text = content.UserDescription;
            DescriptionScrollViewer.Visibility = Visibility.Visible;
        }

        try
        {
            Task imageTask = LoadImageAsync(item, generation, cancellationToken);
            Task authorTask = LoadAuthorAsync(item, generation, cancellationToken);
            Task voteTask = LoadVoteAsync(item, generation, cancellationToken);
            await Task.WhenAll(imageTask, authorTask, voteTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppLog.Debug("创意工坊横向卡片加载已取消。", "Workshop.HorizontalCard");
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "加载创意工坊横向卡片失败。", "Workshop.HorizontalCard");
            if (IsCurrentLoad(item.Id, generation))
                ImageLoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadImageAsync(
        Item item,
        int generation,
        CancellationToken cancellationToken)
    {
        ImageLoadingBar.SetProgressInstant(0.12);
        var image = await WorkshopPage.DownloadImageAsBitmapAsync(item, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentLoad(item.Id, generation)) return;

        BgImage.Source = image;
        ImageLoadingBar.SetProgress(1, 120);
        await Task.Delay(100, cancellationToken);
        if (IsCurrentLoad(item.Id, generation))
            ImageLoadingPanel.Visibility = Visibility.Collapsed;
    }

    private async Task LoadAuthorAsync(
        Item item,
        int generation,
        CancellationToken cancellationToken)
    {
        WorkshopAuthorInfo author = await WorkshopAuthorInfo.LoadAsync(item.Owner.Id);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentLoad(item.Id, generation) || _item.Owner.Id != item.Owner.Id) return;

        AuthorName.Text = author.Name;
        AuthorName.ToolTip = author.Name;
        AuthorAvatar.Source = author.Avatar;
        AuthorPanel.Visibility = Visibility.Visible;
    }

    private async Task LoadVoteAsync(
        Item item,
        int generation,
        CancellationToken cancellationToken)
    {
        var vote = await item.GetUserVote();
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentLoad(item.Id, generation)) return;

        _userVote = vote?.VotedUp == true
            ? true
            : vote?.VotedDown == true
                ? false
                : null;
        UpdateFeedbackState();
    }

    private bool IsCurrentLoad(Steamworks.Data.PublishedFileId itemId, int generation) =>
        generation == _loadGeneration &&
        _hasItem &&
        _item.Id.Equals(itemId) &&
        IsLoaded;

    private void CancelCurrentLoad()
    {
        _loadGeneration++;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private void ResetVisualState()
    {
        _audioItems.Clear();
        BgImage.Source = null;
        ItemTitle.Text = string.Empty;
        ItemTitle.ToolTip = null;
        SteamScoreText.Text = "★ --";
        AudioCountText.Text = string.Empty;
        ResourceSizeText.Text = string.Empty;
        ResourceDescriptionText.Text = string.Empty;
        DescriptionScrollViewer.Visibility = Visibility.Collapsed;
        DescriptionScrollViewer.ScrollToTop();
        AudioScrollViewer.ScrollToTop();
        AuthorPanel.Visibility = Visibility.Hidden;
        AuthorName.Text = string.Empty;
        AuthorName.ToolTip = null;
        AuthorAvatar.Source = null;
        ImageLoadingPanel.Visibility = Visibility.Visible;
        _userVote = null;
        _isFavorite = false;
        UpdateFeedbackState();
    }

    private void UpdateSubscribeButtons()
    {
        SubscribeButton.Visibility = _isSubscribed
            ? Visibility.Collapsed
            : Visibility.Visible;
        UnsubscribeButton.Visibility = _isSubscribed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void SubscribeButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeSubscriptionAsync(subscribe: true);

    private async void UnsubscribeButton_Click(object sender, RoutedEventArgs e) =>
        await ChangeSubscriptionAsync(subscribe: false);

    private async Task ChangeSubscriptionAsync(bool subscribe)
    {
        if (!_hasItem) return;

        Item item = _item;
        int generation = _loadGeneration;
        SubscribeButton.IsEnabled = false;
        UnsubscribeButton.IsEnabled = false;

        string progressKey = subscribe ? "正在订阅" : "正在退订";
        MainWindow.Inst.AddNotification(
            () => LanguageManager.Inst.GetString("通知"),
            () => $"{LanguageManager.Inst.GetString(progressKey)} : {item.Title}",
            LabelStatus.Warning,
            3.5f);

        bool success = subscribe
            ? await item.Subscribe()
            : await item.Unsubscribe();

        if (!IsCurrentLoad(item.Id, generation)) return;

        if (success)
        {
            _isSubscribed = subscribe;
            UpdateSubscribeButtons();
        }

        string resultKey = success
            ? subscribe ? "已订阅" : "已退订"
            : subscribe ? "订阅失败" : "退订失败";
        MainWindow.Inst.AddNotification(
            () => LanguageManager.Inst.GetString("通知"),
            () => $"{LanguageManager.Inst.GetString(resultKey)} : {item.Title}",
            success ? LabelStatus.Success : LabelStatus.Error,
            3.5f);

        SubscribeButton.IsEnabled = true;
        UnsubscribeButton.IsEnabled = true;
    }

    private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasItem || sender is not Button button || button.Tag is not string actionTag)
            return;

        Item item = _item;
        int generation = _loadGeneration;
        string action = actionTag switch
        {
            "Upvote" => LanguageManager.Inst.GetString("好评"),
            "Downvote" => LanguageManager.Inst.GetString("差评"),
            _ => LanguageManager.Inst.GetString("喜欢")
        };

        bool success = actionTag switch
        {
            "Upvote" => await item.Vote(true) == Result.OK,
            "Downvote" => await item.Vote(false) == Result.OK,
            _ => _isFavorite
                ? await item.RemoveFavorite()
                : await item.AddFavorite()
        };

        if (!IsCurrentLoad(item.Id, generation)) return;

        if (success)
        {
            if (actionTag == "Upvote") _userVote = true;
            else if (actionTag == "Downvote") _userVote = false;
            else _isFavorite = !_isFavorite;
            UpdateFeedbackState();
        }

        MainWindow.Inst.AddNotification(
            () => LanguageManager.Inst.GetString("通知"),
            () => success
                ? $"{action} : {item.Title}"
                : $"{action} {LanguageManager.Inst.GetString("失败")} : {item.Title}",
            success ? LabelStatus.Success : LabelStatus.Error,
            3.5f);
    }

    private void UpdateFeedbackState()
    {
        SetFeedbackState(UpvoteIcon, _userVote == true, "Success");
        SetFeedbackState(DownvoteIcon, _userVote == false, "Error");
        SetFeedbackState(FavoriteIcon, _isFavorite, "Warning");
    }

    private static void SetFeedbackState(Path icon, bool active, string brushKey) =>
        icon.SetResourceReference(
            Shape.FillProperty,
            active ? brushKey : "AccentColor");

    private void OpenWorkshopPage_Click(object sender, RoutedEventArgs e)
    {
        if (_hasItem)
            SteamFriends.OpenWebOverlay(_item.Url);
    }

    private void CopyWorkshopUrl_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasItem) return;

        Clipboard.SetText(_item.Url);
        MainWindow.Inst.AddNotification(
            () => LanguageManager.Inst.GetString("通知"),
            () => LanguageManager.Inst.GetString("已复制") + "URL",
            LabelStatus.Success,
            3.5f);
    }

    private void ReportWorkshopItem_Click(object sender, RoutedEventArgs e)
    {
        if (_hasItem)
            SteamFriends.OpenWebOverlay(_item.Url);
    }

    private void AuthorName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_hasItem)
            SteamFriends.OpenUserOverlay(_item.Owner.Id, "steamid");
    }

    /// <summary>
    /// 内容确实需要纵向滚动时始终吞掉滚轮，包括已经抵达顶部或底部的情况；
    /// 内容无需滚动时不处理事件，让外层横向浏览器接管。
    /// </summary>
    private void NestedScrollViewer_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0.5)
            return;

        double targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset - e.Delta,
            0,
            scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }
}
