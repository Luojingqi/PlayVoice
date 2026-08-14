using NAudio.CoreAudioApi;
using PlayVoice.Audio;
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
            if (physicalLoudspeaker?.ID == id)
                PhysicalLoudspeaker = null;
            if (virtualLoudspeaker?.ID == id)
                VirtualLoudspeaker = null;
            OnLoudspeakerRemove?.Invoke(id, name);
        };
        equipmentLoder.OnLoudspeakerAdd += (id, name) => OnLoudspeakerAdd?.Invoke(id, name);
        equipmentLoder.OnMicrophoneRemove += (id, name) =>
        {
            if (physicalMicrophone?.ID == id)
                PhysicalMicrophone = null;
            if (virtualMicrophone?.ID == id)
                VirtualMicrophone = null;
            OnMicrophoneRemove?.Invoke(id, name);
        };
        equipmentLoder.OnMicrophoneAdd += (id, name) => OnMicrophoneAdd?.Invoke(id, name);
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
        set
        {
            var lastDevice = physicalLoudspeaker;
            if ((value == null && lastDevice == null)
                || (value != null && lastDevice != null && value.ID == lastDevice.ID))
            {
                if (!ReferenceEquals(value, lastDevice))
                    value?.Dispose();
                return;
            }
            GlobalData.Inst.StopForEquipmentChange();
            physicalLoudspeaker = value;
            GlobalData.Inst.Config.PhysicalLoudspeakerID = physicalLoudspeaker?.ID;
            GlobalData.Inst.Config.Save();
            CheckAllEquipmentState();
            PhysicalLoudspeakerOnChanged?.Invoke(lastDevice, physicalLoudspeaker);
            lastDevice?.Dispose();
            GlobalData.Inst.NotifyStartupRequirementsChanged();
        }
    }
    private MMDevice physicalLoudspeaker;

    private bool physicalLoudspeakerState = false;
    public bool PhysicalLoudspeakerState
    {
        get => physicalLoudspeakerState;
        private set
        {
            if (value == physicalLoudspeakerState) return;
            physicalLoudspeakerState = value;
            PhysicalLoudspeakerStateChange?.Invoke(value);
        }
    }
    public event Action<bool> PhysicalLoudspeakerStateChange;
    public void CheckPhysicalLoudspeakerState()
    {
        PhysicalLoudspeakerState =
            physicalLoudspeaker != null
            && !EquipmentLoder.IsCableEquipment(physicalLoudspeaker)
            && (virtualLoudspeaker == null || physicalLoudspeaker.ID != virtualLoudspeaker.ID);
    }
    /// <summary>
    /// 虚拟扬声器
    /// </summary>
    public MMDevice VirtualLoudspeaker
    {
        get => virtualLoudspeaker;
        set
        {
            var lastDevice = virtualLoudspeaker;
            if ((value == null && lastDevice == null)
                || (value != null && lastDevice != null && value.ID == lastDevice.ID))
            {
                if (!ReferenceEquals(value, lastDevice))
                    value?.Dispose();
                return;
            }
            GlobalData.Inst.StopForEquipmentChange();
            virtualLoudspeaker = value;
            GlobalData.Inst.Config.VirtualLoudspeakerID = virtualLoudspeaker?.ID;
            GlobalData.Inst.Config.Save();
            CheckAllEquipmentState();
            VirtualLoudspeakerOnChanged?.Invoke(lastDevice, virtualLoudspeaker);
            lastDevice?.Dispose();
            GlobalData.Inst.NotifyStartupRequirementsChanged();
        }
    }
    private MMDevice virtualLoudspeaker;

    private bool virtualLoudspeakerState = false;
    public bool VirtualLoudspeakerState
    {
        get => virtualLoudspeakerState;
        private set
        {
            if (value == virtualLoudspeakerState) return;
            virtualLoudspeakerState = value;
            VirtualLoudspeakerStateChange?.Invoke(value);
        }
    }
    public event Action<bool> VirtualLoudspeakerStateChange;
    private void CheckVirtualLoudspeakerState()
    {
        VirtualLoudspeakerState =
            virtualLoudspeaker != null
            && EquipmentLoder.IsCableEquipment(virtualLoudspeaker)
            && (physicalLoudspeaker == null || virtualLoudspeaker.ID != physicalLoudspeaker.ID);
    }
    /// <summary>
    /// 物理麦克风
    /// </summary>
    public MMDevice PhysicalMicrophone
    {
        get => physicalMicrophone;
        set
        {
            var lastDevice = physicalMicrophone;
            if ((value == null && lastDevice == null)
                || (value != null && lastDevice != null && value.ID == lastDevice.ID))
            {
                if (!ReferenceEquals(value, lastDevice))
                    value?.Dispose();
                return;
            }
            GlobalData.Inst.StopForEquipmentChange();
            physicalMicrophone = value;
            var config = GlobalData.Inst.Config;
            string microphoneID = physicalMicrophone?.ID;
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
            config.Save();
            CheckAllEquipmentState();
            PhysicalMicrophoneOnChanged?.Invoke(lastDevice, physicalMicrophone);
            lastDevice?.Dispose();
            GlobalData.Inst.NotifyStartupRequirementsChanged();
        }
    }
    private MMDevice physicalMicrophone;

    private bool physicalMicrophoneState = false;
    public bool PhysicalMicrophoneState
    {
        get => physicalMicrophoneState;
        private set
        {
            if (value == physicalMicrophoneState) return;
            physicalMicrophoneState = value;
            PhysicalMicrophoneStateChange?.Invoke(value);
        }
    }
    public event Action<bool> PhysicalMicrophoneStateChange;
    private void CheckPhysicalMicrophoneState()
    {
        PhysicalMicrophoneState =
            physicalMicrophone != null
            && !EquipmentLoder.IsCableEquipment(physicalMicrophone)
            && (virtualMicrophone == null || physicalMicrophone.ID != virtualMicrophone.ID);
    }
    /// <summary>
    /// 虚拟麦克风
    /// </summary>
    public MMDevice VirtualMicrophone
    {
        get => virtualMicrophone;
        set
        {
            var lastDevice = virtualMicrophone;
            if ((value == null && lastDevice == null)
                || (value != null && lastDevice != null && value.ID == lastDevice.ID))
            {
                if (!ReferenceEquals(value, lastDevice))
                    value?.Dispose();
                return;
            }
            GlobalData.Inst.StopForEquipmentChange();
            virtualMicrophone = value;
            GlobalData.Inst.Config.VirtualMicrophoneID = virtualMicrophone?.ID;
            GlobalData.Inst.Config.Save();
            CheckAllEquipmentState();
            VirtualMicrophoneOnChanged?.Invoke(lastDevice, virtualMicrophone);
            lastDevice?.Dispose();
            GlobalData.Inst.NotifyStartupRequirementsChanged();
        }
    }
    private MMDevice virtualMicrophone;

    private bool virtualMicrophoneState = false;
    public bool VirtualMicrophoneState
    {
        get => virtualMicrophoneState;
        private set
        {
            if (value == virtualMicrophoneState) return;
            virtualMicrophoneState = value;
            VirtualMicrophoneStateChange?.Invoke(value);
        }
    }
    public event Action<bool> VirtualMicrophoneStateChange;
    private void CheckVirtualMicrophoneState()
    {
        VirtualMicrophoneState =
            virtualMicrophone != null
            && EquipmentLoder.IsCableEquipment(virtualMicrophone)
            && (physicalMicrophone == null || virtualMicrophone.ID != physicalMicrophone.ID);
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
        if (LoudspeakerDic.ContainsKey(id))
            return equipmentLoder.Enumerator.GetDevice(id);
        return null;
    }
    public MMDevice GetMicrophone(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (MicrophoneDic.ContainsKey(id))
            return equipmentLoder.Enumerator.GetDevice(id);
        return null;
    }


    public event Action<string, string> OnLoudspeakerRemove;
    public event Action<string, string> OnLoudspeakerAdd;
    public event Action<string, string> OnMicrophoneRemove;
    public event Action<string, string> OnMicrophoneAdd;

}
