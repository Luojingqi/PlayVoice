#nullable enable

namespace PlayVoice.Diagnostics;

internal static class TaskExtensions
{
    public static async void Forget(this Task task, string source)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AppLog.Debug("异步操作已取消。", source);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "未等待的异步操作执行失败。", source);
        }
    }
}
