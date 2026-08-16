using Steamworks.Ugc;

namespace PlayVoice.Pages.Workshop;

/// <summary>
/// 将 Steam 每次返回的 50 条数据拆分成每页 10 条的界面内部页。
/// 同一查询会话内缓存已请求的 Steam 数据页，五个相邻内部页共享一次请求。
/// </summary>
internal sealed class WorkshopInternalPagination
{
    internal const int ItemsPerInternalPage = 10;
    internal const int ItemsPerSteamPage = 50;

    private readonly Dictionary<int, CachedSteamPage> _steamPages = new();
    private string _queryIdentity = string.Empty;

    public void BeginQuery(string queryIdentity)
    {
        if (string.Equals(_queryIdentity, queryIdentity, StringComparison.Ordinal))
            return;

        _queryIdentity = queryIdentity;
        _steamPages.Clear();
    }

    public int GetSteamPageNumber(int internalPageNumber)
    {
        int normalizedPage = Math.Max(1, internalPageNumber);
        int globalItemIndex = (normalizedPage - 1) * ItemsPerInternalPage;
        return globalItemIndex / ItemsPerSteamPage + 1;
    }

    public void StoreSteamPage(
        int steamPageNumber,
        IReadOnlyList<Item> items,
        int totalItemCount)
    {
        _steamPages[steamPageNumber] = new CachedSteamPage(
            items.ToArray(),
            Math.Max(0, totalItemCount));
    }

    public bool TryGetInternalPage(
        int internalPageNumber,
        out IReadOnlyList<Item> items,
        out int totalInternalPages)
    {
        int steamPageNumber = GetSteamPageNumber(internalPageNumber);
        if (!_steamPages.TryGetValue(steamPageNumber, out CachedSteamPage? steamPage) ||
            steamPage == null)
        {
            items = Array.Empty<Item>();
            totalInternalPages = 0;
            return false;
        }

        int internalPageIndex = Math.Max(0, internalPageNumber - 1);
        int itemOffset = internalPageIndex %
            (ItemsPerSteamPage / ItemsPerInternalPage) * ItemsPerInternalPage;

        items = steamPage.Items
            .Skip(itemOffset)
            .Take(ItemsPerInternalPage)
            .ToArray();
        totalInternalPages = steamPage.TotalItemCount == 0
            ? 0
            : (int)Math.Ceiling(
                (double)steamPage.TotalItemCount / ItemsPerInternalPage);
        return true;
    }

    public void Clear()
    {
        _queryIdentity = string.Empty;
        _steamPages.Clear();
    }

    private sealed class CachedSteamPage
    {
        public IReadOnlyList<Item> Items { get; }

        public int TotalItemCount { get; }

        public CachedSteamPage(IReadOnlyList<Item> items, int totalItemCount)
        {
            Items = items;
            TotalItemCount = totalItemCount;
        }
    }
}
