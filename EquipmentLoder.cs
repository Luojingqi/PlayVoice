using Microsoft.Win32;
using NAudio.CoreAudioApi;
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
        Update();
        Thread thread = new(() =>
        {
            while (true)
            {
                Thread.Sleep(UpdateTimeInterval);
                Application.Current?.Dispatcher.Invoke(Update);
            }
        });
        thread.Start();
    }
    private Dictionary<string, string> newLoudspeakerDic = new();

    private Dictionary<string, string> newMicrophoneDic = new();

    public int UpdateTimeInterval { get; set; } = 500;

    private void Update()
    {
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
        foreach (var device in devices)
        {
            switch (device.DataFlow)
            {
                case DataFlow.Render:
                    newLoudspeakerDic.Add(device.ID, device.FriendlyName);
                    break;
                case DataFlow.Capture:
                    newMicrophoneDic.Add(device.ID, device.FriendlyName);
                    break;
            }
            device.Dispose();
        }
        foreach (var kv in equipment.LoudspeakerDic)
            if (!newLoudspeakerDic.ContainsKey(kv.Key))
                OnLoudspeakerRemove?.Invoke(kv.Key, kv.Value);


        foreach (var kv in equipment.MicrophoneDic)
            if (!newMicrophoneDic.ContainsKey(kv.Key))
                OnMicrophoneRemove?.Invoke(kv.Key, kv.Value);


        foreach (var kv in newLoudspeakerDic)
            if (!equipment.LoudspeakerDic.ContainsKey(kv.Key))
                OnLoudspeakerAdd?.Invoke(kv.Key, kv.Value);

        foreach (var kv in newMicrophoneDic)
            if (!equipment.MicrophoneDic.ContainsKey(kv.Key))
                OnMicrophoneAdd?.Invoke(kv.Key, kv.Value);

        equipment.LoudspeakerDic.Clear();
        foreach (var kv in newLoudspeakerDic)
            equipment.LoudspeakerDic.Add(kv.Key, kv.Value);

        equipment.MicrophoneDic.Clear();
        foreach (var kv in newMicrophoneDic)
            equipment.MicrophoneDic.Add(kv.Key, kv.Value);

        newLoudspeakerDic.Clear();
        newMicrophoneDic.Clear();
    }

    public event Action<string, string> OnLoudspeakerRemove;
    public event Action<string, string> OnLoudspeakerAdd;
    public event Action<string, string> OnMicrophoneRemove;
    public event Action<string, string> OnMicrophoneAdd;


    public static bool IsCableEquipment(MMDevice device)
    {
        if (device.DeviceFriendlyName.ToLower().Contains("cable") == true)
            return true;
        return false;
    }

    private static string VbcablePath = "VBCABLE_Driver_Pack45/VBCABLE_Setup_x64.exe";
    private static string VbcableCert_Win10_x64_Path = "VBCABLE_Driver_Pack45/vbaudio_cable64_win10.cat";
    public static async Task<int> InstallSilentAsync()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

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

    public static async Task<int> UninstallSilentAsync()
    {
        int exitCode = await RunProcessAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VbcablePath), "-u -h");
        if (exitCode == -1) return exitCode;
        GlobalData.Inst.Equipment.PhysicalLoudspeaker = null;
        GlobalData.Inst.Equipment.PhysicalMicrophone = null;
        GlobalData.Inst.Equipment.VirtualLoudspeaker = null;
        GlobalData.Inst.Equipment.VirtualMicrophone = null;
        await RestartAudioServicesAsync();
        return exitCode;
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
            Console.WriteLine($"Error running process {fileName}: {ex.Message}");
            return -1;
        }
    }

    public static bool IsVBCableInstalled()
    {
        var equipment = GlobalData.Inst.Equipment;
        foreach (var device in equipment.LoudspeakerDic)
            if (device.Value.ToLower().Contains("cable"))
                return true;
        foreach (var device in equipment.MicrophoneDic)
            if (device.Value.ToLower().Contains("cable"))
                return true;
        return false;
    }
}
