using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace PlayVoice;

/// <summary>
/// 记录并恢复当前用户的 Windows 默认播放与录音端点。
/// </summary>
internal sealed class DefaultAudioEndpointSnapshot
{
    private const int RestoreAttemptCount = 10;
    private const int RestoreRetryDelayMilliseconds = 300;

    private static readonly DataFlow[] DataFlows =
    {
        DataFlow.Render,
        DataFlow.Capture,
    };

    private static readonly Role[] Roles =
    {
        Role.Console,
        Role.Multimedia,
        Role.Communications,
    };

    private readonly List<DefaultEndpoint> endpoints;

    private DefaultAudioEndpointSnapshot(List<DefaultEndpoint> endpoints)
    {
        this.endpoints = endpoints;
    }

    public static DefaultAudioEndpointSnapshot Capture()
    {
        List<DefaultEndpoint> endpoints = new();
        using MMDeviceEnumerator enumerator = new();

        foreach (DataFlow dataFlow in DataFlows)
        {
            foreach (Role role in Roles)
            {
                try
                {
                    using MMDevice device = enumerator.GetDefaultAudioEndpoint(dataFlow, role);
                    endpoints.Add(new(device.ID, dataFlow, role));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"[警告] 无法记录默认音频设备（{dataFlow}/{role}）：{exception.Message}");
                }
            }
        }

        return new(endpoints);
    }

    public async Task RestoreAsync()
    {
        if (endpoints.Count == 0)
            return;

        string lastError = null;
        for (int attempt = 1; attempt <= RestoreAttemptCount; attempt++)
        {
            if (TryRestore(out lastError))
                return;

            if (attempt < RestoreAttemptCount)
                await Task.Delay(RestoreRetryDelayMilliseconds);
        }

        Console.WriteLine($"[错误] 恢复 Windows 默认音频设备失败：{lastError}");
    }

    private bool TryRestore(out string error)
    {
        IPolicyConfig policy = null;
        try
        {
            policy = (IPolicyConfig)new PolicyConfigClient();
            foreach (DefaultEndpoint endpoint in endpoints)
            {
                int result = policy.SetDefaultEndpoint(endpoint.DeviceId, endpoint.Role);
                if (result < 0)
                {
                    error = $"{endpoint.DataFlow}/{endpoint.Role}，HRESULT=0x{result:X8}";
                    return false;
                }
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (policy != null)
                Marshal.FinalReleaseComObject(policy);
        }
    }

    private readonly record struct DefaultEndpoint(
        string DeviceId,
        DataFlow DataFlow,
        Role Role);

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient
    {
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig]
        int GetMixFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            out IntPtr format);

        [PreserveSig]
        int GetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool isDefault,
            out IntPtr format);

        [PreserveSig]
        int ResetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int SetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr endpointFormat,
            IntPtr mixFormat);

        [PreserveSig]
        int GetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool isDefault,
            out long defaultPeriod,
            out long minimumPeriod);

        [PreserveSig]
        int SetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            ref long period);

        [PreserveSig]
        int GetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr mode);

        [PreserveSig]
        int SetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr mode);

        [PreserveSig]
        int GetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr key,
            IntPtr value);

        [PreserveSig]
        int SetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr key,
            IntPtr value);

        [PreserveSig]
        int SetDefaultEndpoint(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            Role role);

        [PreserveSig]
        int SetEndpointVisibility(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool visible);
    }
}
