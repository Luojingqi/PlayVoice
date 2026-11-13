using System.Globalization;
using Lepo.i18n;

namespace PlayVoice.Resources.Language;

internal static class LocalizationCatalog
{
    internal static readonly string[] CultureNames = { "zh-CN", "zh-TW", "en-US", "ru-RU", "es-ES", "pt-BR", "ja-JP", "ko-KR", "de-DE" };
    private static readonly ILocalizationProvider Provider = CreateProvider();

    private static ILocalizationProvider CreateProvider()
    {
        var builder = new LocalizationBuilder();
        builder.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        foreach (string name in CultureNames)
            builder.FromResource(typeof(LocalizationCatalog).Assembly, "PlayVoice.Resources.Language.Languages", CultureInfo.GetCultureInfo(name));
        return builder.Build();
    }

    internal static CultureInfo ResolveCulture(string name) => CultureInfo.GetCultureInfo(
        CultureNames.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)) ?? "en-US");

    internal static void SetCulture(CultureInfo culture) => Provider.SetCulture(ResolveCulture(culture.Name));

    internal static string Translate(string key, CultureInfo culture = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        var selected = ResolveCulture((culture ?? Provider.GetCulture()).Name);
        var set = Provider.GetLocalizationSet(selected, null);
        if (set != null && set.Strings.Any(entry => entry.Key == key)) return set[key] ?? key;
        return Provider.GetLocalizationSet(CultureInfo.GetCultureInfo("en-US"), null)?[key] ?? key;
    }
}
