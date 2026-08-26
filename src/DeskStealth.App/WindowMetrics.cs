using DeskStealth.Rendering;

namespace DeskStealth;

internal static class WindowMetrics
{
    public static Size ClientSizeFor(int dpi, float userScale)
    {
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi));
        }

        if (!float.IsFinite(userScale) || userScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userScale));
        }

        var dpiScale = dpi / 96f;
        return new Size(
            Math.Max(1, (int)Math.Round(GameRenderer.CanvasWidth * userScale * dpiScale)),
            Math.Max(1, (int)Math.Round(GameRenderer.CanvasHeight * userScale * dpiScale)));
    }

    public static Point ClampLocation(Rectangle workingArea, Size windowSize, Point requestedLocation)
    {
        var maximumX = Math.Max(workingArea.Left, workingArea.Right - windowSize.Width);
        var maximumY = Math.Max(workingArea.Top, workingArea.Bottom - windowSize.Height);
        return new Point(
            Math.Clamp(requestedLocation.X, workingArea.Left, maximumX),
            Math.Clamp(requestedLocation.Y, workingArea.Top, maximumY));
    }
}
