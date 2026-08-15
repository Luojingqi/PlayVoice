using NAudio.CoreAudioApi;
using PlayVoice.Audio;
using PlayVoice.Diagnostics;
using static PlayVoice.Equipment;

namespace PlayVoice;

/// <summary>
/// 系统设备
/// </summary>
internal class Equipment
{
    public Equipment()
    {
        equipmentLoder = new(this);
        equipmentLoder.OnLoudspeakerRemove += (id, name) =>
        {
            if (DeviceHasId(physicalLoudspeaker, id))
                PhysicalLoudspeaker = null;
            if (DeviceHasId(virtualLoudspeaker, id))
                VirtualLoudspeaker = null;
            InvokeDeviceListChangedSafely(
                OnLoudspeakerRemove,
                id,
                name,
                "Equipment.Loudspeaker.Removed");
        };
        equipmentLoder.OnLoudspeakerAdd += (id, name) =>
            InvokeDeviceListChangedSafely(
                OnLoudspeakerAdd,
                id,
                name,
                "Equipment.Loudspeaker.Added");
        equipmentLoder.OnMicrophoneRemove += (id, name) =>
        {
            if (DeviceHasId(physicalMicrophone, id))
                PhysicalMicrophone = null;
            if (DeviceHasId(virtualMicrophone, id))
                VirtualMicrophone = null;
            InvokeDeviceListChangedSafely(
                OnMicrophoneRemove,
                id,
                name,
                "Equipment.Microphone.Removed");
        };
        equipmentLoder.OnMicrophoneAdd += (id, name) =>
            InvokeDeviceListChangedSafely(
                OnMicrophoneAdd,
                id,
                name,
                "Equipment.Microphone.Added");
        equipmentLoder.OnVBCableInstallationChanged += isInstalled =>
            GlobalData.Inst.NotifyStartupRequirementsChanged();
    }

    public void Init()
    {
        var config = GlobalData.Inst.Config;
        if (!string.IsNullOrWhiteSpace(config.PhysicalLoudspeakerID))
            PhysicalLoudspeaker = GetLoudspeaker(config.PhysicalLoudspeakerID) ?? null;
        if (!string.IsNullOrWhiteSpace(config.PhysicalMicrophoneID))
            PhysicalMicrophone = GetMicrophone(config.PhysicalMicrophoneID) ?? null;
        if (!string.IsNullOrWhiteSpace(config.VirtualLoudspeakerID))
            VirtualLoudspeaker = GetLoudspeaker(config.VirtualLoudspeakerID) ?? null;
        if (!string.IsNullOrWhiteSpace(config.VirtualMicrophoneID))
            VirtualMicrophone = GetMicrophone(config.VirtualMicrophoneID) ?? null;
    }


    /// <summary>
    /// 物理扬声器
    /// </summary>
    public MMDevice PhysicalLoudspeaker
    {
        get => physicalLoudspeaker;
        set => TrySetPhysicalLoudspeaker(value);
    }
    private MMDevice physicalLoudspeaker;

    public bool TrySetPhysicalLoudspeaker(MMDevice device)
    {
        MMDevice lastDevice = physicalLoudspeaker;
        if (IsSameDevice(device, lastDevice))
        {
            DisposeDuplicateDevice(device, lastDevice);
            return true;
        }

        if (!TryGetDeviceId(device, out string deviceID, out Exception idException))
        {
            DisposeDevice(device, "Equipment.PhysicalLoudspeaker.Rejected");
            AppLog.Error(
                idException,
                "读取物理扬声器 ID 失败，已保留原设备。",
                "Equipment.PhysicalLoudspeaker");
            return false;
        }

        GlobalData.Inst.StopForEquipmentChange();
        if (!GlobalData.Inst.AudioProxy.TrySwitchPhysicalLoudspeaker(
            device, out Exception exception))
        {
            DisposeDevice(device, "Equipment.PhysicalLoudspeaker.Rejected");
            AppLog.Error(
                exception,
                "绑定物理扬声器失败，已保留原设备。",
                "Equipment.PhysicalLoudspeaker");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return false;
        }

        physicalLoudspeaker = device;
        GlobalData.Inst.Config.PhysicalLoudspeakerID = deviceID;
        GlobalData.Inst.Config.Save();
        CheckAllEquipmentState();
        InvokeDeviceChangedSafely(
            PhysicalLoudspeakerOnChanged,
            lastDevice,
            physicalLoudspeaker,
            "Equipment.PhysicalLoudspeaker.Changed");
        DisposeDevice(lastDevice, "Equipment.PhysicalLoudspeaker.Replaced");
        GlobalData.Inst.NotifyStartupRequirementsChanged();
        return true;
    }

    private bool physicalLoudspeakerState = false;
    public bool PhysicalLoudspeakerState
    {
        get => physicalLoudspeakerState;
        private set
        {
            if (value == physicalLoudspeakerState) return;
            physicalLoudspeakerState = value;
            InvokeStateChangedSafely(
                PhysicalLoudspeakerStateChange,
                value,
                "Equipment.PhysicalLoudspeaker.State");
        }
    }
    public event Action<bool> PhysicalLoudspeakerStateChange;
    public void CheckPhysicalLoudspeakerState()
    {
        PhysicalLoudspeakerState =
            physicalLoudspeaker != null
            && HasReadableId(physicalLoudspeaker)
            && MatchesCableRole(physicalLoudspeaker, requiresCable: false)
            && !HaveSameId(physicalLoudspeaker, virtualLoudspeaker);
    }
    /// <summary>
    /// 虚拟扬声器
    /// </summary>
    public MMDevice VirtualLoudspeaker
    {
        get => virtualLoudspeaker;
        set => TrySetVirtualLoudspeaker(value);
    }
    private MMDevice virtualLoudspeaker;

    public bool TrySetVirtualLoudspeaker(MMDevice device)
    {
        MMDevice lastDevice = virtualLoudspeaker;
        if (IsSameDevice(device, lastDevice))
        {
            DisposeDuplicateDevice(device, lastDevice);
            return true;
        }

        try
        {
            GlobalData.Inst.StopForEquipmentChange();
            virtualLoudspeaker = device;
            GlobalData.Inst.Config.VirtualLoudspeakerID = GetDeviceIdOrThrow(
                device,
                "Equipment.VirtualLoudspeaker");
            GlobalData.Inst.Config.Save();
            CheckAllEquipmentState();
            InvokeDeviceChangedSafely(
                VirtualLoudspeakerOnChanged,
                lastDevice,
                virtualLoudspeaker,
                "Equipment.VirtualLoudspeaker.Changed");
            DisposeDevice(lastDevice, "Equipment.VirtualLoudspeaker.Replaced");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return true;
        }
        catch (Exception exception)
        {
            virtualLoudspeaker = lastDevice;
            GlobalData.Inst.Config.VirtualLoudspeakerID = GetDeviceIdOrNull(
                lastDevice,
                "Equipment.VirtualLoudspeaker.Rollback");
            GlobalData.Inst.Config.Save();
            DisposeDevice(device, "Equipment.VirtualLoudspeaker.Rejected");
            CheckAllEquipmentState();
            AppLog.Error(
                exception,
                "绑定虚拟扬声器失败，已恢复原设备。",
                "Equipment.VirtualLoudspeaker");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return false;
        }
    }

    private bool virtualLoudspeakerState = false;
    public bool VirtualLoudspeakerState
    {
        get => virtualLoudspeakerState;
        private set
        {
            if (value == virtualLoudspeakerState) return;
            virtualLoudspeakerState = value;
            InvokeStateChangedSafely(
                VirtualLoudspeakerStateChange,
                value,
                "Equipment.VirtualLoudspeaker.State");
        }
    }
    public event Action<bool> VirtualLoudspeakerStateChange;
    private void CheckVirtualLoudspeakerState()
    {
        VirtualLoudspeakerState =
            virtualLoudspeaker != null
            && HasReadableId(virtualLoudspeaker)
            && MatchesCableRole(virtualLoudspeaker, requiresCable: true)
            && !HaveSameId(virtualLoudspeaker, physicalLoudspeaker);
    }
    /// <summary>
    /// 物理麦克风
    /// </summary>
    public MMDevice PhysicalMicrophone
    {
        get => physicalMicrophone;
        set => TrySetPhysicalMicrophone(value);
    }
    private MMDevice physicalMicrophone;

    public bool TrySetPhysicalMicrophone(MMDevice device)
    {
        MMDevice lastDevice = physicalMicrophone;
        if (IsSameDevice(device, lastDevice))
        {
            DisposeDuplicateDevice(device, lastDevice);
            return true;
        }

        var config = GlobalData.Inst.Config;
        double? lastMicrophoneLufs = config.MicrophoneLufs;
        string lastMicrophoneLufsDeviceID = config.MicrophoneLufsDeviceID;

        try
        {
            GlobalData.Inst.StopForEquipmentChange();
            physicalMicrophone = device;
            ApplyPhysicalMicrophoneConfig(GetDeviceIdOrThrow(
                device,
                "Equipment.PhysicalMicrophone"));
            config.Save();
            CheckAllEquipmentState();
            InvokeDeviceChangedSafely(
                PhysicalMicrophoneOnChanged,
                lastDevice,
                physicalMicrophone,
                "Equipment.PhysicalMicrophone.Changed");
            DisposeDevice(lastDevice, "Equipment.PhysicalMicrophone.Replaced");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return true;
        }
        catch (Exception exception)
        {
            physicalMicrophone = lastDevice;
            config.PhysicalMicrophoneID = GetDeviceIdOrNull(
                lastDevice,
                "Equipment.PhysicalMicrophone.Rollback");
            config.MicrophoneLufs = lastMicrophoneLufs;
            config.MicrophoneLufsDeviceID = lastMicrophoneLufsDeviceID;
            config.Save();
            DisposeDevice(device, "Equipment.PhysicalMicrophone.Rejected");
            CheckAllEquipmentState();
            AppLog.Error(
                exception,
                "绑定物理麦克风失败，已恢复原设备。",
                "Equipment.PhysicalMicrophone");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return false;
        }
    }

    private static void ApplyPhysicalMicrophoneConfig(string microphoneID)
    {
        var config = GlobalData.Inst.Config;
        config.PhysicalMicrophoneID = microphoneID;
        config.MicrophoneLufsByDeviceID ??= new(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(microphoneID)
            && config.MicrophoneLufsByDeviceID.TryGetValue(
                microphoneID, out double microphoneLufs))
        {
            config.MicrophoneLufs = microphoneLufs;
            config.MicrophoneLufsDeviceID = microphoneID;
        }
        else
        {
            config.MicrophoneLufs = null;
            config.MicrophoneLufsDeviceID = null;
        }
    }

    private bool physicalMicrophoneState = false;
    public bool PhysicalMicrophoneState
    {
        get => physicalMicrophoneState;
        private set
        {
            if (value == physicalMicrophoneState) return;
            physicalMicrophoneState = value;
            InvokeStateChangedSafely(
                PhysicalMicrophoneStateChange,
                value,
                "Equipment.PhysicalMicrophone.State");
        }
    }
    public event Action<bool> PhysicalMicrophoneStateChange;
    private void CheckPhysicalMicrophoneState()
    {
        PhysicalMicrophoneState =
            physicalMicrophone != null
            && HasReadableId(physicalMicrophone)
            && MatchesCableRole(physicalMicrophone, requiresCable: false)
            && !HaveSameId(physicalMicrophone, virtualMicrophone);
    }
    /// <summary>
    /// 虚拟麦克风
    /// </summary>
    public MMDevice VirtualMicrophone
    {
        get => virtualMicrophone;
        set => TrySetVirtualMicrophone(value);
    }
    private MMDevice virtualMicrophone;

    public bool TrySetVirtualMicrophone(MMDevice device)
    {
        MMDevice lastDevice = virtualMicrophone;
        if (IsSameDevice(device, lastDevice))
        {
            DisposeDuplicateDevice(device, lastDevice);
            return true;
        }

        try
        {
            GlobalData.Inst.StopForEquipmentChange();
            virtualMicrophone = device;
            GlobalData.Inst.Config.VirtualMicrophoneID = GetDeviceIdOrThrow(
                device,
                "Equipment.VirtualMicrophone");
            GlobalData.Inst.Config.Save();
            CheckAllEquipmentState();
            InvokeDeviceChangedSafely(
                VirtualMicrophoneOnChanged,
                lastDevice,
                virtualMicrophone,
                "Equipment.VirtualMicrophone.Changed");
            DisposeDevice(lastDevice, "Equipment.VirtualMicrophone.Replaced");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return true;
        }
        catch (Exception exception)
        {
            virtualMicrophone = lastDevice;
            GlobalData.Inst.Config.VirtualMicrophoneID = GetDeviceIdOrNull(
                lastDevice,
                "Equipment.VirtualMicrophone.Rollback");
            GlobalData.Inst.Config.Save();
            DisposeDevice(device, "Equipment.VirtualMicrophone.Rejected");
            CheckAllEquipmentState();
            AppLog.Error(
                exception,
                "绑定虚拟麦克风失败，已恢复原设备。",
                "Equipment.VirtualMicrophone");
            GlobalData.Inst.NotifyStartupRequirementsChanged();
            return false;
        }
    }

    private bool virtualMicrophoneState = false;
    public bool VirtualMicrophoneState
    {
        get => virtualMicrophoneState;
        private set
        {
            if (value == virtualMicrophoneState) return;
            virtualMicrophoneState = value;
            InvokeStateChangedSafely(
                VirtualMicrophoneStateChange,
                value,
                "Equipment.VirtualMicrophone.State");
        }
    }
    public event Action<bool> VirtualMicrophoneStateChange;
    private void CheckVirtualMicrophoneState()
    {
        VirtualMicrophoneState =
            virtualMicrophone != null
            && HasReadableId(virtualMicrophone)
            && MatchesCableRole(virtualMicrophone, requiresCable: true)
            && !HaveSameId(virtualMicrophone, physicalMicrophone);
    }

    public void CheckAllEquipmentState()
    {
        CheckPhysicalLoudspeakerState();
        CheckVirtualLoudspeakerState();
        CheckPhysicalMicrophoneState();
        CheckVirtualMicrophoneState();
    }

    private EquipmentLoder equipmentLoder;

    public event Action<MMDevice, MMDevice> PhysicalLoudspeakerOnChanged;
    public event Action<MMDevice, MMDevice> VirtualLoudspeakerOnChanged;
    public event Action<MMDevice, MMDevice> PhysicalMicrophoneOnChanged;
    public event Action<MMDevice, MMDevice> VirtualMicrophoneOnChanged;

    /// <summary>
    /// ID=>Name
    /// </summary>
    public Dictionary<string, string> LoudspeakerDic { get; set; } = new();
    /// <summary>
    /// ID=>Name
    /// </summary>
    public Dictionary<string, string> MicrophoneDic { get; set; } = new();

    public MMDevice GetLoudspeaker(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            if (LoudspeakerDic.ContainsKey(id))
                return equipmentLoder.Enumerator.GetDevice(id);
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                exception,
                $"获取扬声器设备失败：{id}",
                "Equipment.GetLoudspeaker");
        }
        return null;
    }
    public MMDevice GetMicrophone(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            if (MicrophoneDic.ContainsKey(id))
                return equipmentLoder.Enumerator.GetDevice(id);
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                exception,
                $"获取麦克风设备失败：{id}",
                "Equipment.GetMicrophone");
        }
        return null;
    }

    private static bool IsSameDevice(MMDevice device, MMDevice currentDevice)
    {
        if (device == null || currentDevice == null)
            return device == null && currentDevice == null;

        return HaveSameId(device, currentDevice);
    }

    private static bool HaveSameId(MMDevice firstDevice, MMDevice secondDevice)
    {
        if (firstDevice == null || secondDevice == null) return false;
        return TryGetDeviceId(firstDevice, out string firstID, out _)
            && TryGetDeviceId(secondDevice, out string secondID, out _)
            && firstID == secondID;
    }

    private static bool DeviceHasId(MMDevice device, string id)
    {
        return TryGetDeviceId(device, out string deviceID, out _)
            && deviceID == id;
    }

    private static bool HasReadableId(MMDevice device)
    {
        return TryGetDeviceId(device, out _, out _);
    }

    private static bool MatchesCableRole(MMDevice device, bool requiresCable)
    {
        return EquipmentLoder.TryIsCableEquipment(device, out bool isCable)
            && isCable == requiresCable;
    }

    private static string GetDeviceIdOrNull(MMDevice device, string source)
    {
        if (TryGetDeviceId(device, out string id, out Exception exception))
            return id;

        AppLog.Warning(exception, "读取音频设备 ID 失败。", source);
        return null;
    }

    private static string GetDeviceIdOrThrow(MMDevice device, string source)
    {
        if (TryGetDeviceId(device, out string id, out Exception exception))
            return id;

        throw new InvalidOperationException(
            $"无法读取音频设备 ID。Source={source}",
            exception);
    }

    private static bool TryGetDeviceId(
        MMDevice device,
        out string id,
        out Exception exception)
    {
        id = null;
        exception = null;
        if (device == null) return true;

        try
        {
            id = device.ID;
            return true;
        }
        catch (Exception idException)
        {
            exception = idException;
            return false;
        }
    }

    private static void DisposeDuplicateDevice(MMDevice device, MMDevice currentDevice)
    {
        if (!ReferenceEquals(device, currentDevice))
            DisposeDevice(device, "Equipment.Duplicate");
    }

    private static void DisposeDevice(MMDevice device, string source)
    {
        if (device == null) return;
        try
        {
            device.Dispose();
        }
        catch (Exception exception)
        {
            AppLog.Warning(exception, "释放音频设备失败。", source);
        }
    }

    private static void InvokeDeviceChangedSafely(
        Action<MMDevice, MMDevice> handlers,
        MMDevice oldDevice,
        MMDevice newDevice,
        string source)
    {
        if (handlers == null) return;
        foreach (Action<MMDevice, MMDevice> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(oldDevice, newDevice);
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "处理音频设备变更事件失败。", source);
            }
        }
    }

    private static void InvokeStateChangedSafely(
        Action<bool> handlers,
        bool state,
        string source)
    {
        if (handlers == null) return;
        foreach (Action<bool> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(state);
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, "处理音频设备状态事件失败。", source);
            }
        }
    }

    private static void InvokeDeviceListChangedSafely(
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
                AppLog.Error(exception, "处理音频设备列表事件失败。", source);
            }
        }
    }


    public event Action<string, string> OnLoudspeakerRemove;
    public event Action<string, string> OnLoudspeakerAdd;
    public event Action<string, string> OnMicrophoneRemove;
    public event Action<string, string> OnMicrophoneAdd;

}
