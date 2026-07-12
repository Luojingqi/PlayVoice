using Microsoft.Win32;
using PlayVoice.Resources.Language;
using System.Collections.ObjectModel;
using System.Windows;
namespace PlayVoice.Resources.Themes;

public static class ThemeManager
{
    public static readonly ThemeEnum Default = ThemeEnum.Dark;
    public static ThemeInfo NowTheme => nowNowTheme;
    private static ThemeInfo nowNowTheme;
    public enum ThemeEnum
    {
        Light,
        Dark,
        System
    }

    public static void Init()
    {
        themeList = new()
        {
            new ThemeInfo(ThemeEnum.Light, "浅色",new Uri("Resources/Themes/DefaultLightTheme.xaml", UriKind.Relative)) ,
            new ThemeInfo(ThemeEnum.Dark, "深色", new Uri("Resources/Themes/DefaultDarkTheme.xaml", UriKind.Relative)) ,
            new ThemeInfo(ThemeEnum.System, "系统", null)
        };
        var dict = Application.Current.Resources.MergedDictionaries;
        NowThemeResourceDictionary = new ResourceDictionary();
        dict.Add(NowThemeResourceDictionary);
        LanguageManager.Inst.CultureChanged += (a, b) =>
        {
            foreach (var theme in themeList)
                theme.UpdateName();
        };
    }
    private static ResourceDictionary NowThemeResourceDictionary;
    public static void SwitchTheme(ThemeEnum themeEnum)
    {
        var themeInfo = ThemeList.FirstOrDefault(t => t.Theme == themeEnum);
        if (themeInfo.Theme != ThemeEnum.System)
            NowThemeResourceDictionary.Source = themeInfo.Url;
        else
        {
            var systemThemeEnum = GetSystemTheme();
            var systemThemeInfo = ThemeList.FirstOrDefault(t => t.Theme == systemThemeEnum);
            NowThemeResourceDictionary.Source = systemThemeInfo.Url;
        }
        nowNowTheme = themeInfo;
        ThemeChanged?.Invoke(themeInfo);
        GlobalData.Inst.Config.Theme = themeEnum;
        GlobalData.Inst.Config.Save();
    }

    public static event Action<ThemeInfo> ThemeChanged;

    public static List<ThemeInfo> ThemeList => themeList;
    private static List<ThemeInfo> themeList;


    public class ThemeInfo
    {
        public ThemeEnum Theme { get; set; }
        public Uri Url { get; set; }
        private string nameKey;
        public string Name { get; set; }
        public int Index => (int)Theme;
        public ThemeInfo(ThemeEnum theme, string nameKey, Uri url)
        {
            Theme = theme;
            this.nameKey = nameKey;
            UpdateName();
            Url = url;
        }
        public void UpdateName()
        {
            Name = LanguageManager.Inst.GetString(nameKey);
        }
    }

    public static ThemeEnum GetSystemTheme()
    {
        const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string registryValue = "AppsUseLightTheme";
        try
        {
            var value = (int)Registry.GetValue(registryKey, registryValue, 1);
            return value == 0 ? ThemeEnum.Dark : ThemeEnum.Light;
        }
        catch
        {
            return ThemeEnum.Light;
        }
    }
}