using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using DeskStealth.Core;

namespace DeskStealth.Rendering;

internal sealed class GameRenderer : IDisposable
{
    public const int CanvasWidth = MapDefinition.CanvasWidth;
    public const int CanvasHeight = MapDefinition.CanvasHeight;

    private static readonly Color HudBackground = Color.FromArgb(255, 5, 15, 31);
    private static readonly Color FloorDark = Color.FromArgb(255, 8, 23, 43);
    private static readonly Color FloorLight = Color.FromArgb(255, 11, 29, 52);
    private static readonly Color Grid = Color.FromArgb(255, 14, 39, 70);
    private static readonly Color Wall = Color.FromArgb(255, 31, 52, 82);
    private static readonly Color WallEdge = Color.FromArgb(255, 112, 137, 168);
    private static readonly Color WallShadow = Color.FromArgb(255, 9, 18, 34);
    private static readonly Color Cyan = Color.FromArgb(255, 79, 220, 242);
    private static readonly Color CyanSoft = Color.FromArgb(255, 141, 238, 249);
    private static readonly Color Folder = Color.FromArgb(255, 247, 199, 68);
    private static readonly Color Coral = Color.FromArgb(255, 225, 88, 100);
    private static readonly Color Green = Color.FromArgb(255, 105, 211, 137);
    private static readonly Color TextPrimary = Color.FromArgb(255, 233, 240, 249);
    private static readonly Color TextMuted = Color.FromArgb(255, 126, 146, 173);

    private readonly AssetCatalog _assets = new();
    private readonly Font _hudFont = CreateFont("Consolas", 7f, FontStyle.Bold);
    private readonly Font _smallFont = CreateFont("Consolas", 6f, FontStyle.Bold);
    private readonly Font _overlayFont = CreateFont("Consolas", 13f, FontStyle.Bold);
    private readonly Font _clockFont = CreateFont("Consolas", 36f, FontStyle.Bold);
    private readonly Font _clockDateFont = CreateFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
    private readonly SolidBrush _floorDarkBrush = new(FloorDark);
    private readonly SolidBrush _floorLightBrush = new(FloorLight);
    private readonly SolidBrush _wallBrush = new(Wall);
    private readonly SolidBrush _coverBrush = new(Color.FromArgb(255, 22, 54, 82));
    private readonly SolidBrush _hudBrush = new(HudBackground);
    private readonly SolidBrush _textBrush = new(TextPrimary);
    private readonly SolidBrush _mutedBrush = new(TextMuted);
    private readonly SolidBrush _cyanBrush = new(Cyan);
    private readonly SolidBrush _coralBrush = new(Coral);
    private readonly SolidBrush _staminaTrackBrush = new(Color.FromArgb(255, 26, 42, 63));
    private readonly Pen _gridPen = new(Grid);
    private readonly Pen _wallEdgePen = new(WallEdge);
    private readonly Pen _wallShadowPen = new(WallShadow, 2);
    private readonly Pen _coverPen = new(CyanSoft) { DashStyle = DashStyle.Dot };
    private readonly Pen _hudDividerPen = new(Color.FromArgb(255, 24, 54, 91));
    private readonly Pen _mutedPen = new(TextMuted);
    private readonly Pen _clockBorderPen = new(Color.FromArgb(130, 24, 62, 103));

    public void Render(
        Bitmap canvas,
        GameSnapshot snapshot,
        MapDefinition map,
        WorldPoint playerFacing,
        DateTimeOffset localNow,
        int bestScore,
        bool animationAlternate)
    {
        using var graphics = Graphics.FromImage(canvas);
        graphics.PageUnit = GraphicsUnit.Pixel;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        if (snapshot.Mode == AppMode.Clock)
        {
            RenderClock(graphics, localNow);
            return;
        }

        RenderGame(graphics, snapshot, map, playerFacing, bestScore, animationAlternate);
    }

    public void Dispose()
    {
        _assets.Dispose();
        _hudFont.Dispose();
        _smallFont.Dispose();
        _overlayFont.Dispose();
        _clockFont.Dispose();
        _clockDateFont.Dispose();
        _floorDarkBrush.Dispose();
        _floorLightBrush.Dispose();
        _wallBrush.Dispose();
        _coverBrush.Dispose();
        _hudBrush.Dispose();
        _textBrush.Dispose();
        _mutedBrush.Dispose();
        _cyanBrush.Dispose();
        _coralBrush.Dispose();
        _staminaTrackBrush.Dispose();
        _gridPen.Dispose();
        _wallEdgePen.Dispose();
        _wallShadowPen.Dispose();
        _coverPen.Dispose();
        _hudDividerPen.Dispose();
        _mutedPen.Dispose();
        _clockBorderPen.Dispose();
    }

    private static Font CreateFont(string familyName, float size, FontStyle style)
    {
        try
        {
            return new Font(familyName, size, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Pixel);
        }
    }

    private void RenderClock(Graphics graphics, DateTimeOffset localNow)
    {
        graphics.Clear(HudBackground);
        using var gridPen = new Pen(Color.FromArgb(70, Grid), 1);
        for (var x = 0; x < CanvasWidth; x += 16)
        {
            graphics.DrawLine(gridPen, x, 0, x, CanvasHeight);
        }

        for (var y = 0; y < CanvasHeight; y += 16)
        {
            graphics.DrawLine(gridPen, 0, y, CanvasWidth, y);
        }

        var timeText = localNow.ToString("HH:mm");
        using var timeBrush = new SolidBrush(TextPrimary);
        using var accentBrush = new SolidBrush(Cyan);
        using var mutedBrush = new SolidBrush(TextMuted);
        using var centered = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(timeText, _clockFont, timeBrush, new RectangleF(0, 34, CanvasWidth, 66), centered);
        graphics.FillRectangle(accentBrush, 57, 105, 142, 2);

        var dateText = localNow.ToString("yyyy-MM-dd  dddd");
        graphics.DrawString(dateText, _clockDateFont, mutedBrush, new RectangleF(0, 111, CanvasWidth, 24), centered);
        graphics.DrawRectangle(_clockBorderPen, 2, 2, CanvasWidth - 5, CanvasHeight - 5);
    }

    private void RenderGame(
        Graphics graphics,
        GameSnapshot snapshot,
        MapDefinition map,
        WorldPoint playerFacing,
        int bestScore,
        bool animationAlternate)
    {
        DrawFloorAndTiles(graphics, map);
        DrawGuardVision(graphics, snapshot, map);
        DrawExit(graphics, snapshot);
        DrawFolders(graphics, snapshot);
        DrawGuards(graphics, snapshot, animationAlternate);
        DrawPlayer(graphics, snapshot, playerFacing, animationAlternate);
        DrawHud(graphics, snapshot);
        DrawStateFeedback(graphics, snapshot, bestScore);
    }

    private void DrawFloorAndTiles(Graphics graphics, MapDefinition map)
    {
        graphics.Clear(FloorDark);
        for (var row = 0; row < MapDefinition.GridHeight; row++)
        {
            for (var column = 0; column < MapDefinition.GridWidth; column++)
            {
                var x = column * MapDefinition.CellSize;
                var y = MapDefinition.HudHeight + (row * MapDefinition.CellSize);
                var bounds = new Rectangle(x, y, MapDefinition.CellSize, MapDefinition.CellSize);
                graphics.FillRectangle((row + column) % 2 == 0 ? _floorDarkBrush : _floorLightBrush, bounds);
                graphics.DrawRectangle(_gridPen, x, y, MapDefinition.CellSize, MapDefinition.CellSize);

                switch (map.TileAt(column, row))
                {
                    case '#':
                        DrawWallTile(graphics, bounds, column, row);
                        break;
                    case 'C':
                        DrawCoverTile(graphics, bounds);
                        break;
                }
            }
        }
    }

    private void DrawWallTile(Graphics graphics, Rectangle bounds, int column, int row)
    {
        graphics.FillRectangle(_wallBrush, bounds);
        graphics.DrawLine(_wallEdgePen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        graphics.DrawLine(_wallEdgePen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
        graphics.DrawLine(_wallShadowPen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

        if (_assets.OfficeTiles is not null && row is > 0 and < MapDefinition.GridHeight - 1)
        {
            var objectCell = (column + row) % 11 switch
            {
                0 => 4,
                5 => 5,
                8 => 6,
                _ => 1,
            };
            _assets.OfficeTiles.Draw(graphics, objectCell % 5, objectCell / 5, Rectangle.Inflate(bounds, -2, -2));
        }
    }

    private void DrawCoverTile(Graphics graphics, Rectangle bounds)
    {
        graphics.FillRectangle(_coverBrush, Rectangle.Inflate(bounds, -1, -2));
        if (_assets.OfficeTiles is not null)
        {
            _assets.OfficeTiles.Draw(graphics, 2, 0, Rectangle.Inflate(bounds, -1, -1));
        }
        else
        {
            graphics.FillRectangle(_mutedBrush, bounds.Left + 2, bounds.Top + 3, bounds.Width - 4, 4);
        }

        graphics.DrawRectangle(_coverPen, Rectangle.Inflate(bounds, -1, -1));
    }

    private static void DrawGuardVision(Graphics graphics, GameSnapshot snapshot, MapDefinition map)
    {
        foreach (var guard in snapshot.Guards)
        {
            var points = new List<PointF> { ToPoint(guard.Position) };
            const int rayCount = 18;
            for (var ray = 0; ray <= rayCount; ray++)
            {
                var angle = guard.FacingDegrees - (GameSession.GuardViewAngleDegrees / 2d) +
                            (GameSession.GuardViewAngleDegrees * ray / rayCount);
                var direction = WorldPoint.FromAngleDegrees(angle);
                var end = guard.Position;
                for (var distance = 2d; distance <= GameSession.GuardViewRange; distance += 2d)
                {
                    var candidate = guard.Position + (direction * distance);
                    if (map.IsSolid(candidate))
                    {
                        break;
                    }

                    end = candidate;
                }

                points.Add(ToPoint(end));
            }

            var fill = guard.SeesPlayer ? Color.FromArgb(100, Coral) : Color.FromArgb(56, Coral);
            using var brush = new SolidBrush(fill);
            using var pen = new Pen(Color.FromArgb(150, Coral)) { DashStyle = DashStyle.Dot };
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillPolygon(brush, points.ToArray());
            graphics.DrawLines(pen, points.Skip(1).ToArray());
            graphics.SmoothingMode = SmoothingMode.None;
        }
    }

    private void DrawExit(Graphics graphics, GameSnapshot snapshot)
    {
        var rectangle = ToRectangle(snapshot.ExitZone);
        if (_assets.OfficeTiles is not null)
        {
            var cell = snapshot.ExitUnlocked ? 9 : 8;
            _assets.OfficeTiles.Draw(graphics, cell % 5, cell / 5, rectangle);
        }
        else
        {
            using var brush = new SolidBrush(snapshot.ExitUnlocked ? Green : WallEdge);
            graphics.FillRectangle(brush, rectangle);
        }

        using var pen = new Pen(snapshot.ExitUnlocked ? Green : TextMuted, 1);
        graphics.DrawRectangle(pen, rectangle);
    }

    private void DrawFolders(Graphics graphics, GameSnapshot snapshot)
    {
        foreach (var folder in snapshot.Folders.Where(folder => !folder.Collected))
        {
            var destination = CenteredRectangle(folder.Position, 14, 14);
            if (_assets.OfficeTiles is not null)
            {
                _assets.OfficeTiles.Draw(graphics, 2, 1, destination);
            }
            else
            {
                using var brush = new SolidBrush(Folder);
                graphics.FillRectangle(brush, destination);
                graphics.DrawRectangle(Pens.White, destination);
            }
        }
    }

    private void DrawGuards(Graphics graphics, GameSnapshot snapshot, bool animationAlternate)
    {
        foreach (var guard in snapshot.Guards)
        {
            var row = FacingRow(guard.FacingDegrees);
            var column = animationAlternate ? 4 : 3;
            var destination = CenteredRectangle(guard.Position, 16, 16);
            if (_assets.Characters is not null)
            {
                _assets.Characters.Draw(graphics, column, row, destination);
            }
            else
            {
                using var brush = new SolidBrush(Coral);
                graphics.FillEllipse(brush, destination);
            }
        }
    }

    private void DrawPlayer(
        Graphics graphics,
        GameSnapshot snapshot,
        WorldPoint playerFacing,
        bool animationAlternate)
    {
        var facingDegrees = Math.Atan2(playerFacing.Y, playerFacing.X) * 180d / Math.PI;
        var row = FacingRow(facingDegrees);
        var column = snapshot.MovementMode == MovementMode.Crouching ? 2 : animationAlternate ? 1 : 0;
        var destination = CenteredRectangle(snapshot.PlayerPosition, 16, 16);
        if (_assets.Characters is not null)
        {
            _assets.Characters.Draw(graphics, column, row, destination);
        }
        else
        {
            using var brush = new SolidBrush(Cyan);
            graphics.FillEllipse(brush, destination);
        }

        using var outline = new Pen(snapshot.IsHidden ? CyanSoft : Cyan);
        graphics.DrawRectangle(outline, Rectangle.Inflate(destination, 1, 1));
        if (snapshot.IsHidden)
        {
            using var hiddenPen = new Pen(CyanSoft) { DashStyle = DashStyle.Dot };
            graphics.DrawArc(hiddenPen, Rectangle.Inflate(destination, 3, 3), 190, 160);
        }
    }

    private void DrawHud(Graphics graphics, GameSnapshot snapshot)
    {
        graphics.FillRectangle(_hudBrush, 0, 0, CanvasWidth, MapDefinition.HudHeight);
        graphics.DrawLine(_hudDividerPen, 0, 15, CanvasWidth, 15);

        var seconds = Math.Max(0, (int)Math.Ceiling(snapshot.RemainingSeconds));
        graphics.DrawString($"{seconds:00}", _hudFont, _textBrush, 5, 3);

        for (var index = 0; index < 3; index++)
        {
            var x = 31 + (index * 10);
            var collected = index < snapshot.Folders.Length && snapshot.Folders[index].Collected;
            using var folderBrush = new SolidBrush(collected ? Folder : Color.FromArgb(255, 38, 54, 76));
            using var folderPen = new Pen(collected ? Color.White : TextMuted);
            graphics.FillRectangle(folderBrush, x, 5, 7, 6);
            graphics.DrawRectangle(folderPen, x, 5, 7, 6);
        }

        graphics.DrawString($"×{3 - snapshot.CollectedFolderCount}", _smallFont, _mutedBrush, 63, 5);

        graphics.FillRectangle(_staminaTrackBrush, 82, 5, 58, 5);
        graphics.FillRectangle(_cyanBrush, 82, 5, (int)Math.Round(58 * snapshot.StaminaFraction), 5);
        graphics.DrawRectangle(_mutedPen, 82, 5, 58, 5);

        const int detectionX = 157;
        for (var segment = 0; segment < 8; segment++)
        {
            var filled = snapshot.DetectionFraction * 8d > segment;
            using var segmentBrush = new SolidBrush(filled ? Coral : Color.FromArgb(255, 28, 43, 66));
            using var segmentPen = new Pen(filled ? Color.FromArgb(255, 255, 153, 150) : TextMuted);
            graphics.FillRectangle(segmentBrush, detectionX + (segment * 11), 4, 9, 6);
            graphics.DrawRectangle(segmentPen, detectionX + (segment * 11), 4, 9, 6);
        }

        for (var catchIndex = 0; catchIndex < snapshot.CatchCount; catchIndex++)
        {
            graphics.FillRectangle(_coralBrush, 145 + (catchIndex * 4), 3, 2, 10);
        }
    }

    private void DrawStateFeedback(Graphics graphics, GameSnapshot snapshot, int bestScore)
    {
        if (snapshot.CatchFreezeRemainingSeconds > 0)
        {
            using var flashPen = new Pen(Coral, 2);
            graphics.DrawRectangle(flashPen, 1, 17, CanvasWidth - 3, CanvasHeight - 19);
        }
        else if (snapshot.GraceRemainingSeconds > 0)
        {
            using var gracePen = new Pen(CyanSoft) { DashStyle = DashStyle.Dot };
            graphics.DrawRectangle(gracePen, 1, 17, CanvasWidth - 3, CanvasHeight - 19);
        }

        switch (snapshot.Mode)
        {
            case AppMode.Ready:
                DrawOverlay(graphics, "READY", snapshot.ReadyRemainingSeconds > 0 ? "1 SEC" : "WASD");
                break;
            case AppMode.Won:
                DrawOverlay(graphics, "CLEAR", $"{snapshot.Score}  BEST {Math.Max(bestScore, snapshot.Score)}");
                DrawFooter(graphics, "ENTER  NEXT");
                break;
            case AppMode.Lost:
                DrawOverlay(graphics, "FAILED", snapshot.CatchCount >= 3 ? "CAUGHT" : "TIME OUT");
                DrawFooter(graphics, "ENTER  RETRY");
                break;
        }
    }

    private void DrawOverlay(Graphics graphics, string title, string detail)
    {
        using var background = new SolidBrush(Color.FromArgb(215, 4, 13, 27));
        using var border = new Pen(Color.FromArgb(220, Cyan));
        using var titleBrush = new SolidBrush(TextPrimary);
        using var detailBrush = new SolidBrush(TextMuted);
        var panel = new Rectangle(73, 56, 110, 45);
        graphics.FillRectangle(background, panel);
        graphics.DrawRectangle(border, panel);
        var titleSize = graphics.MeasureString(title, _overlayFont);
        graphics.DrawString(title, _overlayFont, titleBrush, panel.Left + ((panel.Width - titleSize.Width) / 2f), panel.Top + 7);
        var detailSize = graphics.MeasureString(detail, _smallFont);
        graphics.DrawString(detail, _smallFont, detailBrush, panel.Left + ((panel.Width - detailSize.Width) / 2f), panel.Top + 29);
    }

    private void DrawFooter(Graphics graphics, string text)
    {
        using var background = new SolidBrush(Color.FromArgb(215, 4, 13, 27));
        using var brush = new SolidBrush(TextMuted);
        graphics.FillRectangle(background, 88, 108, 80, 14);
        var size = graphics.MeasureString(text, _smallFont);
        graphics.DrawString(text, _smallFont, brush, 88 + ((80 - size.Width) / 2f), 112);
    }

    private static int FacingRow(double facingDegrees)
    {
        var normalized = (facingDegrees % 360d + 360d) % 360d;
        if (normalized >= 45d && normalized < 135d)
        {
            return 0; // facing down/front
        }

        if (normalized >= 135d && normalized < 225d)
        {
            return 1; // left
        }

        if (normalized >= 225d && normalized < 315d)
        {
            return 3; // up/back
        }

        return 2; // right
    }

    private static PointF ToPoint(WorldPoint point) => new((float)point.X, (float)point.Y);

    private static Rectangle ToRectangle(WorldRect rectangle) => Rectangle.Round(new RectangleF(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)rectangle.Width,
        (float)rectangle.Height));

    private static Rectangle CenteredRectangle(WorldPoint center, int width, int height) => new(
        (int)Math.Round(center.X - (width / 2d)),
        (int)Math.Round(center.Y - (height / 2d)),
        width,
        height);
}
