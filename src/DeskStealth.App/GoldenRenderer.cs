using System.Drawing.Imaging;
using System.Collections.Immutable;
using DeskStealth.Core;
using DeskStealth.Rendering;

namespace DeskStealth;

internal static class GoldenRenderer
{
    public static void Render(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        using var renderer = new GameRenderer();
        using var canvas = new Bitmap(
            GameRenderer.CanvasWidth,
            GameRenderer.CanvasHeight,
            PixelFormat.Format32bppPArgb);
        canvas.SetResolution(96f, 96f);

        var session = new GameSession(MapCatalog.First, AppMode.Playing);
        var fixedTime = new DateTimeOffset(2026, 8, 25, 15, 30, 0, TimeSpan.FromHours(8));
        var referenceState = session.Snapshot with
        {
            PlayerPosition = MapDefinition.CellCenter(1, 7),
            MovementMode = MovementMode.Crouching,
            IsHidden = true,
            Folders = session.Snapshot.Folders
                .Select(folder => folder with { Collected = folder.Index == 2 })
                .ToImmutableArray(),
            RemainingSeconds = 42,
            StaminaFraction = 0.68,
            DetectionFraction = 0.375,
        };

        renderer.Render(
            canvas,
            referenceState,
            session.Map,
            new WorldPoint(0, 1),
            fixedTime,
            bestScore: 0,
            animationAlternate: false);
        canvas.Save(Path.Combine(fullOutputDirectory, "game-golden.png"), ImageFormat.Png);

        session.EnterClock();
        renderer.Render(
            canvas,
            session.Snapshot,
            session.Map,
            new WorldPoint(0, 1),
            fixedTime,
            bestScore: 0,
            animationAlternate: false);
        canvas.Save(Path.Combine(fullOutputDirectory, "clock-golden.png"), ImageFormat.Png);
    }
}
