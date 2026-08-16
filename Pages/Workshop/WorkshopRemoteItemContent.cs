using PlayVoice.Audio;
using Steamworks.Ugc;
using System.Text.Json;

namespace PlayVoice.Pages.Workshop;

/// <summary>
/// 将 Steam 创意工坊物品携带的元数据转换为远程浏览卡片需要的只读内容。
/// 这里集中处理新旧描述格式的兼容，避免各个视图重复解析 JSON。
/// </summary>
internal sealed class WorkshopRemoteItemContent
{
    public IReadOnlyList<WorkshopRemoteAudioItem> AudioItems { get; }

    public string UserDescription { get; }

    public string TotalSize { get; }

    public string AudioCount => $"♪ {AudioItems.Count}";

    private WorkshopRemoteItemContent(
        IReadOnlyList<WorkshopRemoteAudioItem> audioItems,
        string userDescription,
        string totalSize)
    {
        AudioItems = audioItems;
        UserDescription = userDescription;
        TotalSize = totalSize;
    }

    public static WorkshopRemoteItemContent Create(Item item)
    {
        var metadata = string.IsNullOrWhiteSpace(item.Metadata)
            ? null
            : JsonTool.ToObject<ResourceDataConfig.Metadata>(item.Metadata);
        WorkshopDescriptionModel? descriptionModel = TryReadDescriptionModel(item.Description);

        var audioItems = metadata?.ItemList?.Select(resource => new WorkshopRemoteAudioItem(
                resource.Name,
                AudioData.DurationToString(resource.Duration),
                AudioData.SizeToString(resource.Size)))
            .ToList() ?? new List<WorkshopRemoteAudioItem>();

        // 新版描述中也携带音频名称。元数据缺失时仍可展示内容，
        // 时长和大小则留空，兼容早期及不完整的工坊物品。
        if (audioItems.Count == 0 && descriptionModel?.AudioNames != null)
        {
            audioItems.AddRange(descriptionModel.AudioNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new WorkshopRemoteAudioItem(name, string.Empty, string.Empty)));
        }

        long totalSize = metadata?.Size > 0
            ? metadata.Size
            : metadata?.ItemList?.Aggregate(
                0L,
                (total, resource) => checked(total + resource.Size)) ?? 0L;

        string userDescription = descriptionModel?.UserDescription ?? item.Description ?? string.Empty;
        return new WorkshopRemoteItemContent(
            audioItems,
            userDescription,
            totalSize > 0 ? AudioData.SizeToString(totalSize) : string.Empty);
    }

    private static WorkshopDescriptionModel? TryReadDescriptionModel(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        try
        {
            var model = JsonSerializer.Deserialize<WorkshopDescriptionModel>(description);
            if (model?.UserDescription != null && model.AudioNames != null)
                return model;
        }
        catch (JsonException)
        {
            // 旧物品的描述是纯文本，由调用方直接使用。
        }

        return null;
    }
}

internal sealed class WorkshopRemoteAudioItem
{
    public string Title { get; }

    public string Duration { get; }

    public string Size { get; }

    public bool HasDetails =>
        !string.IsNullOrEmpty(Duration) || !string.IsNullOrEmpty(Size);

    public WorkshopRemoteAudioItem(string title, string duration, string size)
    {
        Title = title;
        Duration = duration;
        Size = size;
    }
}
