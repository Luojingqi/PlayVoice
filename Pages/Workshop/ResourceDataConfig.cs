using PlayVoice.Pages.Preset;
using System.Text.Json.Serialization;
using static PlayVoice.Pages.Workshop.ResourceDataConfig;

namespace PlayVoice.Pages.Workshop;

public class ResourceDataConfig
{

    public string Title { get; set; }

    public string Description { get; set; }

    public string ThumbnailFormat { get; set; }

    public Metadata Data { get; set; }

    public class ResourceItem
    {
        public string FileName { get; set; }
        public string FileFormat { get; set; }
        [JsonIgnore]
        public string Name => $"{FileName}{FileFormat}";
        public TimeSpan Duration { get; set; }
        public long Size { get; set; }
    }

    public class Metadata
    {
        public List<ResourceItem> ItemList { get; set; } = new();

        public static Metadata Create(PresetData presetData)
        {
            var metadata = new Metadata();
            for (int i = 0; i < presetData.AudioList.Count; i++)
            {
                var audioData = presetData.AudioList[i];
                var audioDataConfig = presetData.Config.AudioDataConfigList[i];
                var resourceItem = new ResourceItem();
                resourceItem.FileName = audioDataConfig.FileName;
                resourceItem.FileFormat = audioDataConfig.FileFormat;
                resourceItem.Duration = audioData.AudioTrackArray[0].TotalTime;
                resourceItem.Size = audioDataConfig.Size;
                metadata.ItemList.Add(resourceItem);
            }
            return metadata;
        }
    }

}
