using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlayVoice.ConfigVersion;

/// <summary>
/// 将历史 Config 数据逐步迁移到当前版本。
/// </summary>
internal static class ConfigMigrator
{
    /// <summary>
    /// 按迁移完成后的目标版本注册数据结构变化。
    /// 没有注册的版本表示该版本 Config 结构没有变化。
    /// </summary>
    private static readonly IReadOnlyDictionary<int, Action<JsonObject>> Migrations =
        new Dictionary<int, Action<JsonObject>>
        {
            [8] = MigrateToV8
        };

    public static Config Migrate(int version, JsonObject data)
    {
        if (version < 0)
            throw new InvalidDataException($"配置版本不能为负数：{version}");

        if (version > GlobalData.Version)
        {
            throw new NotSupportedException(
                $"配置版本 {version} 高于当前支持的版本 {GlobalData.Version}，请升级程序。");
        }

        JsonObject currentData = data.DeepClone().AsObject();
        int currentVersion = version;

        while (currentVersion < GlobalData.Version)
        {
            int targetVersion = currentVersion + 1;
            if (Migrations.TryGetValue(targetVersion, out Action<JsonObject> migrate))
                migrate(currentData);

            currentVersion = targetVersion;
        }

        return Deserialize<Config>(currentData, currentVersion);
    }

    private static void MigrateToV8(JsonObject data)
    {
        const string propertyName = nameof(Config.MigrationTestValue);
        if (!data.ContainsKey(propertyName))
            data[propertyName] = 8;
    }

    private static T Deserialize<T>(JsonNode data, int version)
    {
        try
        {
            return data.Deserialize<T>()
                ?? throw new InvalidDataException($"配置版本 {version} 的数据为空。");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"配置数据无法按版本 {version} 的模型读取。",
                exception);
        }
    }
}
