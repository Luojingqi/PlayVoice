namespace PlayVoice.Pages.Workshop;

internal static class SteamWorkshopLanguage
{
    internal static readonly (string CultureName, string SteamLanguage)[] SupportedLanguages =
    {
        ("zh-CN", "schinese"),
        ("zh-TW", "tchinese"),
        ("en-US", "english"),
        ("ru-RU", "russian"),
        ("es-ES", "spanish"),
        ("pt-BR", "brazilian"),
        ("ja-JP", "japanese"),
        ("ko-KR", "koreana"),
        ("de-DE", "german")
    };

    internal static string FromClientCulture(string cultureName)
    {
        foreach (var language in SupportedLanguages)
        {
            if (string.Equals(
                language.CultureName,
                cultureName,
                StringComparison.OrdinalIgnoreCase))
                return language.SteamLanguage;
        }

        return "english";
    }
}
