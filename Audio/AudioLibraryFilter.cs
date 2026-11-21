namespace PlayVoice.Audio;

internal static class AudioLibraryFilter
{
    public static bool Matches(AudioDataConfig audio, string search, bool favorites, bool recent) =>
        (!favorites || audio.IsFavorite) && (!recent || audio.LastPlayedAt.HasValue)
        && (string.IsNullOrWhiteSpace(search)
            || audio.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
            || (audio.Tags?.Any(tag => tag != null && tag.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) ?? false));

    public static List<string> ParseTags(string text) => text.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
