using PlayVoice.ConfigVersion;
using System.IO;
using System.Text.Json.Nodes;

namespace PlayVoice;

/// <summary>
/// 统一负责 Config 的读取、迁移和保存。
/// </summary>
internal static class ConfigStorage
{
    private const string FileName = "config.json";

    private static string FilePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);

    public static Config Load()
    {
        if (!File.Exists(FilePath))
        {
            var newConfig = new Config();
            if (!Save(newConfig))
                throw new IOException($"无法创建配置文件：{FilePath}");

            return newConfig;
        }

        JsonNode root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(FilePath))
                ?? throw new InvalidDataException("配置文件内容为空。");
        }
        catch (Exception exception) when (
            exception is not InvalidDataException)
        {
            throw new InvalidDataException("配置文件不是有效的 JSON。", exception);
        }

        DataMetadata<JsonObject> metadata = ReadMetadata(root, out bool isLegacyFormat);
        Config config = ConfigMigrator.Migrate(metadata.Version, metadata.Data);

        if (isLegacyFormat || metadata.Version != GlobalData.Version)
        {
            if (!Save(config))
                throw new IOException("配置迁移成功，但无法保存迁移后的配置文件。");
        }

        return config;
    }

    public static bool Save(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var metadata = new DataMetadata<Config>
        {
            Version = GlobalData.Version,
            Data = config
        };

        return JsonTool.SaveJsonAtomically(FilePath, metadata);
    }

    private static DataMetadata<JsonObject> ReadMetadata(
        JsonNode root,
        out bool isLegacyFormat)
    {
        if (root is not JsonObject rootObject)
            throw new InvalidDataException("配置文件的根节点必须是 JSON 对象。");

        bool hasVersion = rootObject.ContainsKey(nameof(DataMetadata<JsonObject>.Version));
        bool hasData = rootObject.ContainsKey(nameof(DataMetadata<JsonObject>.Data));

        if (!hasVersion && !hasData)
        {
            isLegacyFormat = true;
            return new DataMetadata<JsonObject>
            {
                Version = 0,
                Data = rootObject.DeepClone().AsObject()
            };
        }

        if (!hasVersion || !hasData)
            throw new InvalidDataException("配置元数据不完整，必须同时包含 Version 和 Data。");

        int version;
        try
        {
            version = rootObject[nameof(DataMetadata<JsonObject>.Version)]!.GetValue<int>();
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("配置元数据中的 Version 必须是整数。", exception);
        }

        JsonNode data = rootObject[nameof(DataMetadata<JsonObject>.Data)]
            ?? throw new InvalidDataException("配置元数据中的 Data 不能为空。");

        if (data is not JsonObject dataObject)
            throw new InvalidDataException("配置元数据中的 Data 必须是 JSON 对象。");

        isLegacyFormat = false;
        return new DataMetadata<JsonObject>
        {
            Version = version,
            Data = dataObject.DeepClone().AsObject()
        };
    }
}
