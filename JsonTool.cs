using System.IO;
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
        catch
        {
            Console.WriteLine("Json转换失败");
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
            catch
            {
                Console.WriteLine("Json加载失败");
                return false;
            }
        }
        else
        {
            Console.WriteLine("Json文件不存在");
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
        catch
        {
            Console.WriteLine("Json保存失败");
            return false;
        }
    }

    public static string ToJson<T>(T data, bool writeIndented = true)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = writeIndented });
    }

    public static async Task CopyDirectoryReplaceTrueAsync(string sourceDir, string destinationDir)
    {
        if (Directory.Exists(destinationDir))
        {
            Directory.Delete(destinationDir, true);
        }
        await CopyDirectoryRecursiveAsync(new DirectoryInfo(sourceDir), destinationDir);
    }

    private static async Task CopyDirectoryRecursiveAsync(DirectoryInfo source, string destPath)
    {
        Directory.CreateDirectory(destPath);
        foreach (FileInfo file in source.GetFiles())
        {
            string destFile = Path.Combine(destPath, file.Name);
            using (FileStream sourceStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (FileStream destStream = new FileStream(destFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destStream);
                await Task.Yield();
            }
        }
        foreach (DirectoryInfo subDir in source.GetDirectories())
        {
            string newDest = Path.Combine(destPath, subDir.Name);
            await CopyDirectoryRecursiveAsync(subDir, newDest);
        }
    }
}