using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Core.Services;

public interface IToolbarPlacementService
{
    PixelPoint Calculate(PixelRect selection, PixelRect monitorBounds, PixelRect workArea, PixelSize toolbarSize, CaptureUiState state);
}

public sealed class ToolbarPlacementService : IToolbarPlacementService
{
    private const int OutsideGap = 8;
    private const int InsideGap = 12;
    private const int FullScreenTolerance = 2;

    public PixelPoint Calculate(PixelRect selection, PixelRect monitorBounds, PixelRect workArea, PixelSize toolbarSize, CaptureUiState state)
    {
        var containingBounds = state == CaptureUiState.Selecting ? workArea : monitorBounds;
        if (IsFullScreen(selection, monitorBounds))
        {
            return Clamp(new PixelPoint(monitorBounds.X + ((monitorBounds.Width - toolbarSize.Width) / 2), monitorBounds.Y + InsideGap), toolbarSize, monitorBounds);
        }

        PixelPoint? best = null;
        long bestOverlap = long.MaxValue;
        foreach (var candidate in CreateOutsideCandidates(selection, toolbarSize).Concat(CreateInsideCandidates(selection, toolbarSize)))
        {
            var toolbar = new PixelRect(candidate.X, candidate.Y, toolbarSize.Width, toolbarSize.Height);
            if (!containingBounds.Contains(toolbar))
            {
                continue;
            }

            var overlap = toolbar.IntersectionArea(selection);
            if (overlap == 0)
            {
                return candidate;
            }

            if (overlap < bestOverlap)
            {
                best = candidate;
                bestOverlap = overlap;
            }
        }

        return best ?? Clamp(new PixelPoint(selection.Center.X - (toolbarSize.Width / 2), selection.Top + InsideGap), toolbarSize, containingBounds);
    }

    private static bool IsFullScreen(PixelRect selection, PixelRect monitor) =>
        Math.Abs(selection.Left - monitor.Left) <= FullScreenTolerance &&
        Math.Abs(selection.Top - monitor.Top) <= FullScreenTolerance &&
        Math.Abs(selection.Right - monitor.Right) <= FullScreenTolerance &&
        Math.Abs(selection.Bottom - monitor.Bottom) <= FullScreenTolerance;

    private static IEnumerable<PixelPoint> CreateOutsideCandidates(PixelRect selection, PixelSize toolbar) =>
    [
        new(selection.Center.X - (toolbar.Width / 2), selection.Bottom + OutsideGap),
        new(selection.Center.X - (toolbar.Width / 2), selection.Top - toolbar.Height - OutsideGap),
        new(selection.Right + OutsideGap, selection.Center.Y - (toolbar.Height / 2)),
        new(selection.Left - toolbar.Width - OutsideGap, selection.Center.Y - (toolbar.Height / 2)),
    ];

    private static IEnumerable<PixelPoint> CreateInsideCandidates(PixelRect selection, PixelSize toolbar) =>
    [
        new(selection.Center.X - (toolbar.Width / 2), selection.Top + InsideGap),
        new(selection.Center.X - (toolbar.Width / 2), selection.Bottom - toolbar.Height - InsideGap),
        new(selection.Right - toolbar.Width - InsideGap, selection.Top + InsideGap),
        new(selection.Left + InsideGap, selection.Top + InsideGap),
    ];

    private static PixelPoint Clamp(PixelPoint point, PixelSize toolbar, PixelRect bounds) => new(
        Math.Clamp(point.X, bounds.Left, bounds.Right - toolbar.Width),
        Math.Clamp(point.Y, bounds.Top, bounds.Bottom - toolbar.Height));
}
