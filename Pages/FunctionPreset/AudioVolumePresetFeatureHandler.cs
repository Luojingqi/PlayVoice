using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public sealed class AudioVolumePresetFeatureHandler : IFunctionPresetFeatureHandler
{
    public string FeatureKey => "AudioVolumes";

    public void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset)
    {
        if (functionPreset == null || audioPreset == null)
            return;

        foreach (var audio in audioPreset.AudioList)
        {
            double decibel = functionPreset.GetAudioDecibel(
                audioPreset.Config.Name, audio.Config.Id);
            audio.SetVolume(decibel);
        }
    }

    public void Clear()
    {
    }
}
