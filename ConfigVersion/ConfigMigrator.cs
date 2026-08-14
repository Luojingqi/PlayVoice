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
            [8] = MigrateToV8,
            [9] = MigrateToV9
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
        const string microphoneIDPropertyName = nameof(Config.MicrophoneLufsDeviceID);
        const string loudnessMapPropertyName = nameof(Config.MicrophoneLufsByDeviceID);

        bool hasLoudnessResult = data[nameof(Config.MicrophoneLufs)] != null;
        if (!data.ContainsKey(microphoneIDPropertyName))
        {
            data[microphoneIDPropertyName] = hasLoudnessResult
                ? data[nameof(Config.PhysicalMicrophoneID)]?.DeepClone()
                : null;
        }

        var loudnessByDeviceID = new JsonObject();
        JsonNode microphoneIDNode = data[nameof(Config.MicrophoneLufsDeviceID)];
        JsonNode microphoneLufsNode = data[nameof(Config.MicrophoneLufs)];
        string microphoneID = microphoneIDNode?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(microphoneID))
            microphoneID = data[nameof(Config.PhysicalMicrophoneID)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(microphoneID) && microphoneLufsNode != null)
        {
            loudnessByDeviceID[microphoneID] = microphoneLufsNode.DeepClone();
        }

        if (!data.ContainsKey(loudnessMapPropertyName))
            data[loudnessMapPropertyName] = loudnessByDeviceID;
    }

    private static void MigrateToV9(JsonObject data)
    {
        AddHotkeyIfMissing(nameof(Config.EarAudioToggleHotkey));
        AddHotkeyIfMissing(nameof(Config.EarInToggleHotkey));
        AddHotkeyIfMissing(nameof(Config.AutoMuteToggleHotkey));

        void AddHotkeyIfMissing(string propertyName)
        {
            if (data[propertyName] != null)
                return;

            data[propertyName] = new JsonObject
            {
                [nameof(Hotkey.HotkeyData.Modifiers)] = 0,
                [nameof(Hotkey.HotkeyData.VkCode)] = 0,
                [nameof(Hotkey.HotkeyData.IsMouse)] = false
            };
        }
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
