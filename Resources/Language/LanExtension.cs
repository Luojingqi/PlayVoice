using System.Windows.Data;
using System.Windows.Markup;

namespace PlayVoice.Resources.Language;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LanExtension : MarkupExtension
{
    public LanExtension()
    {
    }

    public LanExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LanguageManager.Inst,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
