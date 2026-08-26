namespace DeskStealth.Core;

/// <summary>A point or vector in the fixed 256 x 160 logical-pixel canvas.</summary>
public readonly record struct WorldPoint(double X, double Y)
{
    public static WorldPoint Zero => new(0, 0);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public WorldPoint Normalized()
    {
        var length = Length;
        return length <= Geometry.Epsilon ? Zero : this / length;
    }

    public static double Distance(WorldPoint left, WorldPoint right) => (left - right).Length;

    public static double Dot(WorldPoint left, WorldPoint right) => (left.X * right.X) + (left.Y * right.Y);

    public static WorldPoint FromAngleDegrees(double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new WorldPoint(Math.Cos(radians), Math.Sin(radians));
    }

    public static WorldPoint operator +(WorldPoint left, WorldPoint right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static WorldPoint operator -(WorldPoint left, WorldPoint right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static WorldPoint operator *(WorldPoint point, double scalar) =>
        new(point.X * scalar, point.Y * scalar);

    public static WorldPoint operator /(WorldPoint point, double scalar) =>
        new(point.X / scalar, point.Y / scalar);
}

/// <summary>An axis-aligned rectangle in logical pixels.</summary>
public readonly record struct WorldRect(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public WorldPoint Center => new(X + (Width / 2d), Y + (Height / 2d));

    public bool Contains(WorldPoint point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public bool ContainsCircle(WorldPoint center, double radius) =>
        center.X - radius >= Left && center.X + radius <= Right &&
        center.Y - radius >= Top && center.Y + radius <= Bottom;

    public bool IntersectsCircle(WorldPoint center, double radius)
    {
        var closestX = Math.Clamp(center.X, Left, Right);
        var closestY = Math.Clamp(center.Y, Top, Bottom);
        var offsetX = center.X - closestX;
        var offsetY = center.Y - closestY;
        return (offsetX * offsetX) + (offsetY * offsetY) < (radius * radius) - Geometry.Epsilon;
    }

    internal bool IntersectsSegment(WorldPoint start, WorldPoint end)
    {
        var tMinimum = 0d;
        var tMaximum = 1d;
        var delta = end - start;

        return Clip(-delta.X, start.X - Left, ref tMinimum, ref tMaximum) &&
               Clip(delta.X, Right - start.X, ref tMinimum, ref tMaximum) &&
               Clip(-delta.Y, start.Y - Top, ref tMinimum, ref tMaximum) &&
               Clip(delta.Y, Bottom - start.Y, ref tMinimum, ref tMaximum) &&
               tMaximum >= Geometry.Epsilon && tMinimum <= 1d - Geometry.Epsilon;
    }

    private static bool Clip(double denominator, double numerator, ref double minimum, ref double maximum)
    {
        if (Math.Abs(denominator) <= Geometry.Epsilon)
        {
            return numerator >= 0;
        }

        var ratio = numerator / denominator;
        if (denominator < 0)
        {
            if (ratio > maximum)
            {
                return false;
            }

            minimum = Math.Max(minimum, ratio);
        }
        else
        {
            if (ratio < minimum)
            {
                return false;
            }

            maximum = Math.Min(maximum, ratio);
        }

        return true;
    }
}

internal static class Geometry
{
    public const double Epsilon = 0.000001d;

    public static double NormalizeDegrees(double degrees)
    {
        degrees %= 360d;
        return degrees < 0 ? degrees + 360d : degrees;
    }

    public static double SignedAngleDifference(double fromDegrees, double toDegrees)
    {
        var difference = NormalizeDegrees(toDegrees) - NormalizeDegrees(fromDegrees);
        if (difference > 180d)
        {
            difference -= 360d;
        }
        else if (difference < -180d)
        {
            difference += 360d;
        }

        return difference;
    }

    public static double AngleDegrees(WorldPoint direction) =>
        NormalizeDegrees(Math.Atan2(direction.Y, direction.X) * 180d / Math.PI);
}
