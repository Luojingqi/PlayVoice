using Microsoft.Win32;
using NAudio.CoreAudioApi;
using PlayVoice.Diagnostics;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PlayVoice;

internal class EquipmentLoder
{
    private MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
    public MMDeviceEnumerator Enumerator => enumerator;
    private Equipment equipment;

    public EquipmentLoder(Equipment equipment)
    {
        this.equipment = equipment;
        UpdateSafely();
        Thread thread = new(() =>
        {
            while (true)
            {
                Thread.Sleep(UpdateTimeInterval);
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted)
                    return;

                try
                {
                    dispatcher.Invoke(UpdateSafely);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (InvalidOperationException) when (dispatcher.HasShutdownStarted)
                {
                    return;
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        exception,
                        "调度音频设备轮询失败，将在下次轮询重试。",
                        "EquipmentLoader.Polling");
                }
            }
        })
        {
            IsBackground = true,
            Name = "PlayVoice.AudioDevicePolling"
        };
        thread.Start();
    }

    private bool? isVBCableInstalled;

    public int UpdateTimeInterval { get; set; } = 500;

    private void UpdateSafely()
    {
        try
        {
            Update();
        }
        catch (Exception exception)
        {
            AppLog.Error(
                exception,
                "音频设备轮询失败，将在下次轮询重试。",
                "EquipmentLoader.Update");
        }
    }

    private void Update()
    {
        var newLoudspeakerDic = new Dictionary<string, string>();
        var newMicrophoneDic = new Dictionary<string, string>();
        bool snapshotComplete = true;
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
        foreach (var device in devices)
        {
            try
            {
                switch (device.DataFlow)
                {
                    case DataFlow.Render:
                        newLoudspeakerDic[device.ID] = device.FriendlyName;
                        break;
                    case DataFlow.Capture:
                        newMicrophoneDic[device.ID] = device.FriendlyName;
                        break;
                }
            }
            catch (Exception exception)
            {
                snapshotComplete = false;
                AppLog.Warning(
                    exception,
                    "读取音频设备信息失败，本轮不会应用设备变化。",
                    "EquipmentLoader.Enumerate");
            }
            finally
            {
                try
                {
                    device.Dispose();
                }
                catch (Exception exception)
                {
                    AppLog.Warning(
                        exception,
                        "释放枚举到的音频设备失败。",
                        "EquipmentLoader.Enumerate");
                }
            }
        }
        if (!snapshotComplete) return;

        foreach (var kv in equipment.LoudspeakerDic)
            if (!newLoudspeakerDic.ContainsKey(kv.Key))
                InvokeDeviceEventSafely(
                    OnLoudspeakerRemove,
                    kv.Key,
                    kv.Value,
                    "EquipmentLoader.LoudspeakerRemoved");


        foreach (var kv in equipment.MicrophoneDic)
            if (!newMicrophoneDic.ContainsKey(kv.Key))
                InvokeDeviceEventSafely(
                    OnMicrophoneRemove,
                    kv.Key,
                    kv.Value,
                    "EquipmentLoader.MicrophoneRemoved");


        foreach (var kv in newLoudspeakerDic)
            if (!equipment.LoudspeakerDic.ContainsKey(kv.Key))
                InvokeDeviceEventSafely(
                    OnLoudspeakerAdd,
                    kv.Key,
                    kv.Value,
                    "EquipmentLoader.LoudspeakerAdded");

        foreach (var kv in newMicrophoneDic)
            if (!equipment.MicrophoneDic.ContainsKey(kv.Key))
                InvokeDeviceEventSafely(
                    OnMicrophoneAdd,
                    kv.Key,
                    kv.Value,
                    "EquipmentLoader.MicrophoneAdded");

        equipment.LoudspeakerDic.Clear();
        foreach (var kv in newLoudspeakerDic)
            equipment.LoudspeakerDic.Add(kv.Key, kv.Value);

        equipment.MicrophoneDic.Clear();
        foreach (var kv in newMicrophoneDic)
            equipment.MicrophoneDic.Add(kv.Key, kv.Value);

        bool currentVBCableInstalled = ContainsCableDevice(
            equipment.LoudspeakerDic, equipment.MicrophoneDic);
        if (isVBCableInstalled.HasValue
            && isVBCableInstalled.Value != currentVBCableInstalled)
        {
            InvokeInstallationChangedSafely(currentVBCableInstalled);
        }
        isVBCableInstalled = currentVBCableInstalled;
    }

    private static void InvokeDeviceEventSafely(
        Action<string, string> handlers,
        string id,
        string name,
        string source)
    {
        if (handlers == null) return;
        foreach (Action<string, string> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(id, name);
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "处理音频设备枚举事件失败。", source);
            }
        }
    }

    private void InvokeInstallationChangedSafely(bool isInstalled)
    {
        if (OnVBCableInstallationChanged == null) return;
        foreach (Action<bool> handler in OnVBCableInstallationChanged.GetInvocationList())
        {
            try
            {
                handler(isInstalled);
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    exception,
                    "处理虚拟声卡安装状态事件失败。",
                    "EquipmentLoader.VBCableState");
            }
        }
    }

    public event Action<string, string> OnLoudspeakerRemove;
    public event Action<string, string> OnLoudspeakerAdd;
    public event Action<string, string> OnMicrophoneRemove;
    public event Action<string, string> OnMicrophoneAdd;
    public event Action<bool> OnVBCableInstallationChanged;


    public static bool IsCableEquipment(MMDevice device)
    {
        return TryIsCableEquipment(device, out bool isCable) && isCable;
    }

    public static bool TryIsCableEquipment(MMDevice device, out bool isCable)
    {
        isCable = false;
        if (device == null) return false;
        try
        {
            isCable = device.DeviceFriendlyName.Contains(
                "cable",
                StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                exception,
                "读取音频设备名称失败，将按非 Cable 设备处理。",
                "EquipmentLoader.DeviceRole");
            return false;
        }
    }

    private static string VbcablePath = "VBCABLE_Driver_Pack45/VBCABLE_Setup_x64.exe";
    private static string VbcableCert_Win10_x64_Path = "VBCABLE_Driver_Pack45/vbaudio_cable64_win10.cat";
    public static async Task<int> InstallSilentAsync()
    {
        DefaultAudioEndpointSnapshot defaultAudioEndpoints =
            DefaultAudioEndpointSnapshot.Capture();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            int exitCode = -1;
            exitCode = await RunProcessAsync("certutil.exe", $"-addstore \"TrustedPublisher\" \"{Path.Combine(baseDir, VbcableCert_Win10_x64_Path)}\"");
            if (exitCode == -1) return exitCode;
            exitCode = await RunProcessAsync(Path.Combine(baseDir, VbcablePath), "-i -h");
            GlobalData.Inst.Equipment.PhysicalLoudspeaker = null;
            GlobalData.Inst.Equipment.PhysicalMicrophone = null;
            GlobalData.Inst.Equipment.VirtualLoudspeaker = null;
            GlobalData.Inst.Equipment.VirtualMicrophone = null;
            if (exitCode == -1) return exitCode;
            await RestartAudioServicesAsync();
            return exitCode;
        }
        finally
        {
            await defaultAudioEndpoints.RestoreAsync();
        }
    }

    public static async Task<int> UninstallSilentAsync()
    {
        GlobalData.Inst.Equipment.PhysicalLoudspeaker = null;
        GlobalData.Inst.Equipment.PhysicalMicrophone = null;
        GlobalData.Inst.Equipment.VirtualLoudspeaker = null;
        GlobalData.Inst.Equipment.VirtualMicrophone = null;
        return await RunProcessAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VbcablePath), "-u -h");
    }

    private static async Task RestartAudioServicesAsync()
    {
        string arguments = "/c net stop audiosrv /y & net stop AudioEndpointBuilder /y & net start AudioEndpointBuilder & net start audiosrv";

        await RunProcessAsync("cmd.exe", arguments);
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        using Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
        try
        {
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            AppLog.Error(
                ex,
                $"运行外部进程失败：{fileName}",
                "EquipmentLoader.Process");
            return -1;
        }
    }

    public static bool IsVBCableInstalled()
    {
        var equipment = GlobalData.Inst.Equipment;
        return ContainsCableDevice(
            equipment.LoudspeakerDic, equipment.MicrophoneDic);
    }

    private static bool ContainsCableDevice(
        IEnumerable<KeyValuePair<string, string>> loudspeakers,
        IEnumerable<KeyValuePair<string, string>> microphones)
    {
        return loudspeakers.Any(device =>
                device.Value.Contains("cable", StringComparison.OrdinalIgnoreCase))
            || microphones.Any(device =>
                device.Value.Contains("cable", StringComparison.OrdinalIgnoreCase));
    }
}
