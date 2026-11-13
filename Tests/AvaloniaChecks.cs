using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using PlayVoice;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.Reflection;
AppBuilder.Configure<DesktopApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
new LanguageManager(); ThemeManager.Init();
var apply=typeof(ThemeManager).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!;
foreach(var mode in new[]{"Light","Dark","System"})
{
 apply.Invoke(null,new object[]{new AppearanceProfile{Mode=mode},"Ocean"});
 var window=new MainWindow();
 window.Measure(new Size(960,640));window.Arrange(new Rect(0,0,960,640));
 if(window.FindControl<ListBox>("Sounds")==null)throw new Exception("Missing library");
 if(window.FindControl<ComboBox>("ThemeChoices")==null)throw new Exception("Missing appearance controls");
 window.Close();
}
Console.WriteLine("PASS: Avalonia AXAML/assets/styles, three theme variants and 960x640 headless layout; no audio devices or desktop windows opened");
