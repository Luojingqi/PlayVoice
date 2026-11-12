namespace PlayVoice { internal static class GlobalData { public const int Version = 9; } }
namespace PlayVoice { public class MainWindow { public static MainWindow Inst { get; } = new(); public bool IsActive => true; } }
namespace PlayVoice.Resources.Themes { public static class ThemeManager { public enum ThemeEnum { Light, Dark, System } } }
namespace PlayVoice.Diagnostics
{
    public static class AppLog
    {
        public static void Error(Exception error, string message, string source) { }
        public static void Warning(Exception error, string message, string source) { }
        public static void Warning(string message, string source) { }
    }
}
