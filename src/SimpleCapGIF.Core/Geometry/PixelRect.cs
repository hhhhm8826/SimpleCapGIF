namespace SimpleCapGIF.Core.Geometry;

public readonly record struct PixelRect
{
    public PixelRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int Left => X;
    public int Top => Y;
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
    public PixelPoint Location => new(X, Y);
    public PixelSize Size => new(Width, Height);
    public PixelPoint Center => new(X + (Width / 2), Y + (Height / 2));

    public bool Contains(PixelRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Intersects(PixelRect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public long IntersectionArea(PixelRect other)
    {
        var width = Math.Max(0, Math.Min(Right, other.Right) - Math.Max(Left, other.Left));
        var height = Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top));
        return (long)width * height;
    }

    public PixelRect ClampInside(PixelRect bounds)
    {
        var width = Math.Min(Width, bounds.Width);
        var height = Math.Min(Height, bounds.Height);
        var x = Math.Clamp(X, bounds.Left, bounds.Right - width);
        var y = Math.Clamp(Y, bounds.Top, bounds.Bottom - height);
        return new PixelRect(x, y, width, height);
    }
}
