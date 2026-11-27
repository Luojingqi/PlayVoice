namespace PlayVoice.Audio;

public sealed class AudioEditSettings
{
    public double StartSeconds { get; set; }
    public double? EndSeconds { get; set; }
    public double FadeInSeconds { get; set; }
    public double FadeOutSeconds { get; set; }

    public double Duration(double originalDuration) => (EndSeconds ?? originalDuration) - StartSeconds;

    internal static double RemainingFadeTime(double duration, double otherFade)
    {
        double available = Math.Max(0, duration - otherFade);
        return available > 0 && available + otherFade > duration ? Math.BitDecrement(available) : available;
    }

    internal static (double FadeIn, double FadeOut) FitFadesToDuration(double fadeIn, double fadeOut, double duration)
    {
        if (!double.IsFinite(duration) || duration <= 0
            || !double.IsFinite(fadeIn) || !double.IsFinite(fadeOut) || fadeIn < 0 || fadeOut < 0
            || fadeIn + fadeOut <= duration)
            return (fadeIn, fadeOut);

        double largestFade = Math.Max(fadeIn, fadeOut);
        double inWeight = fadeIn / largestFade;
        double outWeight = fadeOut / largestFade;
        double fittedFadeIn = duration * (inWeight / (inWeight + outWeight));
        return (fittedFadeIn, RemainingFadeTime(duration, fittedFadeIn));
    }

    public void Validate(double originalDuration, int sampleRate = 0)
    {
        double end = EndSeconds ?? originalDuration;
        if (!double.IsFinite(originalDuration) || originalDuration <= 0
            || !double.IsFinite(StartSeconds) || !double.IsFinite(end)
            || StartSeconds < 0 || end > originalDuration || end <= StartSeconds
            || !double.IsFinite(FadeInSeconds) || !double.IsFinite(FadeOutSeconds)
            || FadeInSeconds < 0 || FadeOutSeconds < 0
            || FadeInSeconds > end - StartSeconds || FadeOutSeconds > end - StartSeconds
            || (sampleRate > 0 && Math.Round(end * sampleRate) <= Math.Round(StartSeconds * sampleRate)))
            throw new ArgumentException("Invalid trim range or fade duration.");
    }
}
