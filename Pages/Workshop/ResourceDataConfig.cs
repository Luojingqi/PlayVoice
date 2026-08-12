using PlayVoice.Pages.Preset;
using System.IO;
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

        /// <summary>
        /// 所有音频资源的总大小（字节）
        /// </summary>
        public long AudioTotalSize { get; set; }

        /// <summary>
        /// ExtendedExplanation 目录内所有文件的总大小（字节）
        /// </summary>
        public long ExtendedExplanationTotalSize { get; set; }

        public static Metadata Create(AudioPresetData presetData, string contentPath = null)
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
                string audioPath = string.IsNullOrEmpty(contentPath)
                    ? null
                    : Path.Combine(contentPath, audioDataConfig.Name);
                resourceItem.Size = audioPath != null && File.Exists(audioPath)
                    ? new FileInfo(audioPath).Length
                    : audioDataConfig.Size;
                metadata.ItemList.Add(resourceItem);
            }

            metadata.AudioTotalSize = metadata.ItemList.Aggregate(
                0L,
                (total, item) => checked(total + item.Size));
            return metadata;
        }
    }

}
