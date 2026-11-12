using System.IO;
using PlayVoice.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PlayVoice;

public static class JsonTool
{

    public static T ToObject<T>(string jsonString)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(jsonString);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "JSON 转换失败。", "JsonTool.Deserialize");
            return default(T);
        }
    }

    public static bool LoadJson<T>(string path, out T data)
    {
        data = default(T);
        if (File.Exists(path))
        {
            string jsonString = File.ReadAllText(path);
            try
            {
                data = JsonSerializer.Deserialize<T>(jsonString);
                return true;
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    exception,
                    $"JSON 加载失败：{path}",
                    "JsonTool.Load");
                return false;
            }
        }
        else
        {
            AppLog.Warning($"JSON 文件不存在：{path}", "JsonTool.Load");
            return false;
        }
    }

    public static bool SaveJson<T>(string path, T data)
    {
        try
        {
            string jsonString = ToJson(data);
            File.WriteAllText(path, jsonString);
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Error(
                exception,
                $"JSON 保存失败：{path}",
                "JsonTool.Save");
            return false;
        }
    }

    /// <summary>
    /// 先完整写入同目录临时文件，再替换目标文件，避免写入中断损坏原文件。
    /// </summary>
    public static bool SaveJsonAtomically<T>(string path, T data)
    {
        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            string jsonString = ToJson(data);
            File.WriteAllText(tempPath, jsonString);
            if (File.Exists(path)) File.Replace(tempPath, path, path + ".bak");
            else File.Move(tempPath, path);
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception cleanupException)
            {
                // 清理临时文件失败不应掩盖最初的保存失败。
                AppLog.Warning(
                    cleanupException,
                    $"清理 JSON 临时文件失败：{tempPath}",
                    "JsonTool.SaveAtomic");
            }

            AppLog.Error(
                exception,
                $"原子保存 JSON 失败：{path}",
                "JsonTool.SaveAtomic");
            return false;
        }
    }

    public static string ToJson<T>(T data, bool writeIndented = true)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = writeIndented });
    }

    public static async Task CopyDirectoryReplaceTrueAsync(string sourceDir, string destinationDir, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(destinationDir))
        {
            Directory.Delete(destinationDir, true);
        }
        await CopyDirectoryRecursiveAsync(new DirectoryInfo(sourceDir), destinationDir, cancellationToken);
    }

    private static async Task CopyDirectoryRecursiveAsync(DirectoryInfo source, string destPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((source.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Cannot copy linked directory: {source.Name}");
        Directory.CreateDirectory(destPath);
        foreach (FileInfo file in source.GetFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Cannot copy linked file: {file.Name}");
            string destFile = Path.Combine(destPath, file.Name);
            using (FileStream sourceStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (FileStream destStream = new FileStream(destFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destStream, cancellationToken);
                await Task.Yield();
            }
        }
        foreach (DirectoryInfo subDir in source.GetDirectories())
        {
            string newDest = Path.Combine(destPath, subDir.Name);
            await CopyDirectoryRecursiveAsync(subDir, newDest, cancellationToken);
        }
    }
}
