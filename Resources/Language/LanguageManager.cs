using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;

namespace PlayVoice.Resources.Language;

public sealed class LanguageManager : INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager = new("PlayVoice.Resources.Language.Languages", typeof(LanguageManager).Assembly);
    private CultureInfo _currentCulture;

    public static LanguageManager Inst { get; private set; }


    public event PropertyChangedEventHandler PropertyChanged;
    /// <summary>
    /// 语言切换触发
    /// </summary>
    public event Action<CultureInfo, LanguageInfo> CultureChanged;
    private LanguageInfo nowLanguageInfo;
    public LanguageInfo NowLanguageInfo => nowLanguageInfo;

    public CultureInfo CurrentCulture => _currentCulture;

    public string this[string key] => GetString(key);

    public void SetCulture(string cultureName)
    {
        SetCulture(CultureInfo.GetCultureInfo(cultureName));
    }

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(_currentCulture, culture))
        {
            return;
        }
        nowLanguageInfo = LanguageList.FirstOrDefault(x => x.Key == culture.Name);
        _currentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        OnPropertyChanged("Item[]");
        CultureChanged?.Invoke(culture, LanguageList.Find(l => l.Key == culture.Name));
        GlobalData.Inst.Config.Language = culture.Name;
        GlobalData.Inst.Config.Save();
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return ResourceManager.GetString(key, _currentCulture) ?? key;
    }

    public string SpliceString(string input, IEnumerable<string> replacements)
    {
        var sb = new StringBuilder();
        int currentIndex = 0;
        string marker = "$";
        int markerLen = marker.Length;
        int valueIndex = 0;

        while (currentIndex < input.Length)
        {
            int pos = input.IndexOf(marker, currentIndex, StringComparison.Ordinal);
            if (pos < 0)
            {
                sb.Append(input, currentIndex, input.Length - currentIndex);
                break;
            }

            sb.Append(input, currentIndex, pos - currentIndex);

            if (valueIndex < replacements.Count())
                sb.Append(replacements.ElementAt(valueIndex++));
            else
                sb.Append(marker);
            currentIndex = pos + markerLen;
        }
        return sb.ToString();
    }
    public string SpliceString(string input, params string[] replacements)
    {
        return SpliceString(input, (IEnumerable<string>)replacements);
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public LanguageManager()
    {
        Inst = this;
    }


    public class LanguageInfo
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public int Index { get; set; }
    }


    public List<LanguageInfo> LanguageList = new()
    {
        new LanguageInfo { Key = "zh-CN", Value = "简体中文", Index = 0 },
        new LanguageInfo { Key = "en-US", Value = "English", Index = 1 },
    };
}
