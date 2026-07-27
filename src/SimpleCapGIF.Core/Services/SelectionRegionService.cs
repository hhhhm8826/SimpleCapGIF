using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Core.Services;

public sealed class SelectionRegionService
{
    public const int MinimumWidth = 160;
    public const int MinimumHeight = 90;

    public static PixelRect ApplyDelta(PixelRect region, PixelRect monitorBounds, ResizeHandle handle, int deltaX, int deltaY)
    {
        if (handle == ResizeHandle.Move)
        {
            return new PixelRect(region.X + deltaX, region.Y + deltaY, region.Width, region.Height).ClampInside(monitorBounds);
        }

        var left = region.Left;
        var top = region.Top;
        var right = region.Right;
        var bottom = region.Bottom;

        if (handle is ResizeHandle.TopLeft or ResizeHandle.Left or ResizeHandle.BottomLeft)
        {
            left = Math.Clamp(left + deltaX, monitorBounds.Left, right - MinimumWidth);
        }

        if (handle is ResizeHandle.TopLeft or ResizeHandle.Top or ResizeHandle.TopRight)
        {
            top = Math.Clamp(top + deltaY, monitorBounds.Top, bottom - MinimumHeight);
        }

        if (handle is ResizeHandle.TopRight or ResizeHandle.Right or ResizeHandle.BottomRight)
        {
            right = Math.Clamp(right + deltaX, left + MinimumWidth, monitorBounds.Right);
        }

        if (handle is ResizeHandle.BottomLeft or ResizeHandle.Bottom or ResizeHandle.BottomRight)
        {
            bottom = Math.Clamp(bottom + deltaY, top + MinimumHeight, monitorBounds.Bottom);
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }
}
