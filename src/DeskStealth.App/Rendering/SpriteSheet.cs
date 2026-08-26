using System.Drawing.Drawing2D;

namespace DeskStealth.Rendering;

internal sealed class SpriteSheet : IDisposable
{
    private readonly Bitmap _bitmap;

    private SpriteSheet(Bitmap bitmap, int columns, int rows)
    {
        _bitmap = bitmap;
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; }

    public int Rows { get; }

    public static SpriteSheet FromBitmap(Bitmap bitmap, int columns, int rows) =>
        new(bitmap, columns, rows);

    public void Draw(Graphics graphics, int column, int row, Rectangle destination)
    {
        if (column < 0 || column >= Columns || row < 0 || row >= Rows)
        {
            return;
        }

        var source = new Rectangle(
            column * _bitmap.Width / Columns,
            row * _bitmap.Height / Rows,
            ((column + 1) * _bitmap.Width / Columns) - (column * _bitmap.Width / Columns),
            ((row + 1) * _bitmap.Height / Rows) - (row * _bitmap.Height / Rows));
        if (source.Width <= 0 || source.Height <= 0 || destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(destination.Width / (double)source.Width, destination.Height / (double)source.Height);
        var drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        var fittedDestination = new Rectangle(
            destination.Left + ((destination.Width - drawWidth) / 2),
            destination.Top + ((destination.Height - drawHeight) / 2),
            drawWidth,
            drawHeight);

        var previousInterpolation = graphics.InterpolationMode;
        var previousPixelOffset = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(_bitmap, fittedDestination, source, GraphicsUnit.Pixel);
        graphics.InterpolationMode = previousInterpolation;
        graphics.PixelOffsetMode = previousPixelOffset;
    }

    public void Dispose() => _bitmap.Dispose();
}
