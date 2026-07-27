using System.Numerics;

namespace SimpleCapGIF.Windows.Capture;

internal static class HdrToneMapCurve
{
    internal const float Knee = 0.75f;

    internal static Vector3 MapToSrgb(Vector3 linearColor)
    {
        var luminance = Math.Max(Vector3.Dot(linearColor, new Vector3(0.2126f, 0.7152f, 0.0722f)), 0f);
        if (luminance > Knee)
        {
            var remainingRange = 1f - Knee;
            var mappedLuminance = Knee + (remainingRange * (1f - MathF.Exp(-(luminance - Knee) / remainingRange)));
            linearColor *= mappedLuminance / luminance;
        }

        return new Vector3(
            LinearToSrgb(Math.Clamp(linearColor.X, 0f, 1f)),
            LinearToSrgb(Math.Clamp(linearColor.Y, 0f, 1f)),
            LinearToSrgb(Math.Clamp(linearColor.Z, 0f, 1f)));
    }

    private static float LinearToSrgb(float value) => value <= 0.0031308f
        ? value * 12.92f
        : (1.055f * MathF.Pow(value, 1f / 2.4f)) - 0.055f;
}
