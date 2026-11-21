using System.Text.Json.Nodes;
using PlayVoice;
using PlayVoice.ConfigVersion;
using PlayVoice.Audio;
using PlayVoice.Hotkey;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var customTheme = PlayVoice.Resources.Themes.AppearanceProfile.Parse("""{"Name":"Test","FontSize":16,"Accent":"#5577CC","Background":"#101010","Foreground":"#EEEEEE"}""");
Check(customTheme.Copy().FontSize == 16, "Theme roundtrip");
foreach (var invalid in new[] { "{\"Version\":99}", "{\"FontSize\":100}", "{\"Accent\":\"red\"}", "{\"FontFamily\":\"https://example.com/font\"}", "{\"Background\":\"#000000\",\"Foreground\":\"#000000\"}" })
{
    try { PlayVoice.Resources.Themes.AppearanceProfile.Parse(invalid); throw new Exception("Invalid theme accepted"); }
    catch (System.IO.InvalidDataException) { }
}
var palettes = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ColorPalettes.json")))!.AsObject();
Check(palettes.Count == 4, "Four distinct color themes required");
foreach (var palette in palettes)
{
    foreach (var mode in new[] { "Light", "Dark", "SurfacesLight", "SurfacesDark" })
    {
        var colors = palette.Value![mode]!.AsArray();
        Check(colors.Count == (mode.StartsWith("Surfaces") ? 3 : 5), "Incomplete theme definition");
        Check(colors.All(c => System.Text.RegularExpressions.Regex.IsMatch(c!.GetValue<string>(), "^#[0-9A-Fa-f]{6}$")), "Invalid theme color");
    }
}
var hotkeys = new HotkeyManager();
var binding = new HotkeyData { VkCode = 65, Modifiers = Win32Modifiers.LControl };
hotkeys.AddHotkey(binding);
Check(hotkeys.IsAssigned(new HotkeyData { VkCode = 65, Modifiers = Win32Modifiers.LControl }, null!), "Duplicate hotkey conflict");
Check(!hotkeys.IsAssigned(binding, binding), "Editing same binding must be allowed");
Check(!hotkeys.IsAssigned(new HotkeyData { VkCode = 65, Modifiers = Win32Modifiers.RControl }, null!), "Left/right modifier distinction");
Check(!hotkeys.IsAssigned(new HotkeyData { VkCode = 65, Modifiers = Win32Modifiers.LControl, IsMouse = true }, null!), "Mouse/keyboard distinction");
hotkeys.RemoveHotkey(binding);
Check(!hotkeys.IsAssigned(binding, null!), "Unregistration");
var sound = new AudioDataConfig { FileName = "Long SOUND", FileFormat = ".wav", Tags = new() { "Game" }, IsFavorite = true };
Check(AudioLibraryFilter.Matches(sound, "sound", true, false), "Case insensitive name search");
Check(AudioLibraryFilter.Matches(sound, "game", false, false), "Tag search");
Check(!AudioLibraryFilter.Matches(sound, "", false, true), "Unplayed excluded from recent");
Check(AudioLibraryFilter.ParseTags(" game, Game，music;; ").SequenceEqual(new[] { "game", "music" }), "Tag normalization");
sound.Tags = new() { null!, "test" };
Check(AudioLibraryFilter.Matches(sound, "test", false, false), "Null external tag entries");
var old = JsonNode.Parse("""{"Language":"zh-CN","MaxPlayingAudioCount":7,"PhysicalMicrophoneID":"old-device","ActiveFunctionPresetName":"game"}""")!.AsObject();
var migrated = ConfigMigrator.Migrate(7, old);
Check(migrated.Language == "zh-CN" && migrated.MaxPlayingAudioCount == 7 && migrated.PhysicalMicrophoneID == "old-device" && migrated.ActiveFunctionPresetName == "game", "Migration must preserve existing settings");
Check(old["Language"]!.GetValue<string>() == "zh-CN", "Migration must not mutate input");
try { ConfigMigrator.Migrate(GlobalData.Version + 1, old); throw new Exception("Future version accepted"); } catch (NotSupportedException) { }
var folder = Path.Combine(Path.GetTempPath(), "PlayVoice-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var path = Path.Combine(folder, "config.json");
    File.WriteAllText(path, old.ToJsonString());
    var loaded = ConfigStorage.Load(path);
    Check(loaded.ColorPalette == "Forest", "Existing configs must receive a default color theme");
    Check(loaded.MaxPlayingAudioCount == 7, "Legacy load");
    Check(File.ReadAllText(path + ".v0.bak") == old.ToJsonString(), "Migration backup must preserve exact original");
    loaded.MaxPlayingAudioCount = 8;
    loaded.ColorPalette = "Ocean";
    Check(ConfigStorage.Save(loaded, path), "Save failed");
    Check(ConfigStorage.Load(path).ColorPalette == "Ocean", "Color theme selection must survive restart");
    loaded.Appearance = customTheme.Copy();
    loaded.CustomThemes[customTheme.Name] = customTheme.Copy();
    Check(ConfigStorage.Save(loaded, path), "Custom appearance save");
    var restoredAppearance = ConfigStorage.Load(path);
    Check(restoredAppearance.Appearance.FontSize == 16 && restoredAppearance.CustomThemes["Test"].Accent == "#5577CC", "Custom themes and font settings must survive restart");
    File.WriteAllText(path, "{broken");
    Check(ConfigStorage.Load(path).MaxPlayingAudioCount == 8, "Backup recovery failed");
    Check(Directory.GetFiles(folder, "config.json.corrupt-*").Length == 1, "Damaged original must be retained");
    File.WriteAllText(path, """{"Version":999,"Data":{}}""");
    try { ConfigStorage.Load(path); throw new Exception("Future config silently rolled back"); } catch (NotSupportedException) { }
    Check(File.ReadAllText(path).Contains("999"), "Future configuration overwritten");
    File.WriteAllText(path, """{"Version":9,"Data":{"UnknownSetting":{"keep":true},"Language":"de-DE"}}""");
    var withUnknown = ConfigStorage.Load(path);
    Check(ConfigStorage.Save(withUnknown, path) && File.ReadAllText(path).Contains("UnknownSetting"), "Unknown fields must survive saves");
    var preset = Path.Combine(folder, "preset.json");
    Check(JsonTool.SaveJsonAtomically(preset, new { Name = "first" }), "Initial save");
    Check(JsonTool.SaveJsonAtomically(preset, new { Name = "second" }), "Replacement");
    Check(File.ReadAllText(preset + ".bak").Contains("first"), "Previous revision backup");
    Console.WriteLine("PASS: custom theme validation/roundtrip/persistence, configuration migration/backup/recovery, filters and hotkey conflicts");
}
finally { Directory.Delete(folder, true); }
