namespace SimpleCapGIF.Core.Geometry;

using System.Text.Json.Serialization;

public readonly record struct PixelSize
{
    [JsonConstructor]
    public PixelSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public long Area => (long)Width * Height;
}
