using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public sealed class AudioVolumePresetFeatureHandler : IFunctionPresetFeatureHandler
{
    public string FeatureKey => "AudioVolumes";

    public void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset)
    {
        if (functionPreset == null || audioPreset == null)
            return;

        var bindings = functionPreset.GetBindingsByAudioId(audioPreset.Config.Name);
        foreach (var audio in audioPreset.AudioList)
        {
            double decibel = bindings[audio.Config.Id].FirstOrDefault()?.Decibel ?? 0;
            audio.SetVolume(decibel);
        }
    }

    public void Clear()
    {
    }
}
