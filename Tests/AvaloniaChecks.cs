using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using PlayVoice;
using PlayVoice.Resources.Language;
using PlayVoice.Resources.Themes;
using System.Reflection;
AppBuilder.Configure<DesktopApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
new LanguageManager(); ThemeManager.Init();
var apply=typeof(ThemeManager).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!;
foreach(var mode in new[]{"Light","Dark","System"})
foreach(var fontSize in new[]{11,13,18})
{
 apply.Invoke(null,new object[]{new AppearanceProfile{Mode=mode,FontSize=fontSize},"Ocean"});
 var window=new MainWindow();
 window.Measure(new Size(960,640));window.Arrange(new Rect(0,0,960,640));
 if(window.FindControl<ListBox>("Sounds")==null)throw new Exception("Missing library");
 if(window.FindControl<ComboBox>("ThemeChoices")==null)throw new Exception("Missing appearance controls");
 foreach(var button in window.GetLogicalDescendants().OfType<Button>().Where(b=>b.Classes.Contains("icon")))
 {
  button.ApplyTemplate();button.Measure(new Size(36,36));button.Arrange(new Rect(0,0,36,36));
  if(button.HorizontalContentAlignment!=HorizontalAlignment.Center || button.VerticalContentAlignment!=VerticalAlignment.Center)
   throw new Exception("Icon button is not centered");
  if(button.Content is Control icon)
  {
   var center=icon.TranslatePoint(new Point(icon.Bounds.Width/2,icon.Bounds.Height/2),button);
   if(center==null || Math.Abs(center.Value.X-18)>.6 || Math.Abs(center.Value.Y-18)>.6)
    throw new Exception($"Off-center icon: {center}");
  }
 }
 foreach(var item in window.FindControl<ListBox>("Navigation")!.Items.OfType<ListBoxItem>())
 {
  if(item.VerticalContentAlignment!=VerticalAlignment.Center)throw new Exception("Navigation row is not centered");
  if(item.Content is StackPanel panel && panel.Children.OfType<Control>().Any(c=>c.VerticalAlignment!=VerticalAlignment.Center))
   throw new Exception("Navigation icon/label alignment mismatch");
 }
 window.Close();
}
Console.WriteLine("PASS: Avalonia AXAML/assets/styles, centered icon/button/navigation geometry, three theme variants and 960x640 headless layout; no audio devices or desktop windows opened");
