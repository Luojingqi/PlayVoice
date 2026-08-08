using PlayVoice.Pages.Preset;

namespace PlayVoice.Pages.FunctionPreset;

public interface IFunctionPresetFeatureHandler
{
    string FeatureKey { get; }

    void Apply(FunctionPresetData functionPreset, AudioPresetData audioPreset);

    void Clear();
}
