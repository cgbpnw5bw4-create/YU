using System.Collections.Immutable;

namespace DeskStealth.Core;

public sealed class GuardDefinition
{
    public GuardDefinition(IEnumerable<WorldPoint> waypoints, double initialFacingDegrees)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        Waypoints = waypoints.ToImmutableArray();
        if (Waypoints.Length < 2)
        {
            throw new ArgumentException("A guard patrol requires at least two waypoints.", nameof(waypoints));
        }

        InitialFacingDegrees = Geometry.NormalizeDegrees(initialFacingDegrees);
    }

    public ImmutableArray<WorldPoint> Waypoints { get; }

    public WorldPoint Spawn => Waypoints[0];

    public double InitialFacingDegrees { get; }
}

/// <summary>
/// An immutable 16 x 9 playfield. Coordinates include the 16-pixel HUD offset,
/// so the playable canvas runs from y=16 through y=160.
/// </summary>
public sealed class MapDefinition
{
    public const int CanvasWidth = 256;
    public const int CanvasHeight = 160;
    public const int HudHeight = 16;
    public const int GridWidth = 16;
    public const int GridHeight = 9;
    public const int CellSize = 16;

    public MapDefinition(
        string id,
        string name,
        IEnumerable<string> tileRows,
        WorldPoint playerSpawn,
        WorldRect exitZone,
        IEnumerable<WorldPoint> folderPositions,
        IEnumerable<GuardDefinition> guards)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tileRows);
        ArgumentNullException.ThrowIfNull(folderPositions);
        ArgumentNullException.ThrowIfNull(guards);

        Id = id;
        Name = name;
        TileRows = tileRows.ToImmutableArray();
        PlayerSpawn = playerSpawn;
        ExitZone = exitZone;
        FolderPositions = folderPositions.ToImmutableArray();
        Guards = guards.ToImmutableArray();

        ValidateShape();

        var solids = ImmutableArray.CreateBuilder<WorldRect>();
        var covers = ImmutableArray.CreateBuilder<WorldRect>();
        for (var row = 0; row < GridHeight; row++)
        {
            for (var column = 0; column < GridWidth; column++)
            {
                var tile = TileRows[row][column];
                var rectangle = CellBounds(column, row);
                if (tile == '#')
                {
                    solids.Add(rectangle);
                }
                else if (tile == 'C')
                {
                    covers.Add(rectangle);
                }
            }
        }

        SolidObstacles = solids.ToImmutable();
        CoverZones = covers.ToImmutable();
        ValidateEntities();
    }

    public string Id { get; }

    public string Name { get; }

    public ImmutableArray<string> TileRows { get; }

    public WorldPoint PlayerSpawn { get; }

    public WorldRect ExitZone { get; }

    public ImmutableArray<WorldPoint> FolderPositions { get; }

    public ImmutableArray<GuardDefinition> Guards { get; }

    public ImmutableArray<WorldRect> SolidObstacles { get; }

    public ImmutableArray<WorldRect> CoverZones { get; }

    public static WorldRect CellBounds(int column, int row)
    {
        if (column is < 0 or >= GridWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (row is < 0 or >= GridHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        return new WorldRect(column * CellSize, HudHeight + (row * CellSize), CellSize, CellSize);
    }

    public static WorldPoint CellCenter(int column, int row) => CellBounds(column, row).Center;

    public char TileAt(int column, int row) => TileRows[row][column];

    public bool IsSolid(WorldPoint center, double radius = 0d)
    {
        if (center.X - radius < 0 || center.X + radius > CanvasWidth ||
            center.Y - radius < HudHeight || center.Y + radius > CanvasHeight)
        {
            return true;
        }

        return radius <= Geometry.Epsilon
            ? SolidObstacles.Any(obstacle => obstacle.Contains(center))
            : SolidObstacles.Any(obstacle => obstacle.IntersectsCircle(center, radius));
    }

    public bool IsInCover(WorldPoint center) => CoverZones.Any(zone => zone.Contains(center));

    public bool HasClearLine(WorldPoint start, WorldPoint end) =>
        !SolidObstacles.Any(obstacle => obstacle.IntersectsSegment(start, end));

    private void ValidateShape()
    {
        if (TileRows.Length != GridHeight)
        {
            throw new ArgumentException($"A map must contain exactly {GridHeight} rows.", nameof(TileRows));
        }

        for (var row = 0; row < GridHeight; row++)
        {
            if (TileRows[row].Length != GridWidth)
            {
                throw new ArgumentException($"Map row {row} must contain exactly {GridWidth} tiles.", nameof(TileRows));
            }

            if (TileRows[row].Any(tile => tile is not ('.' or '#' or 'C')))
            {
                throw new ArgumentException($"Map row {row} contains an unsupported tile.", nameof(TileRows));
            }
        }

        if (TileRows[0].Any(tile => tile != '#') || TileRows[^1].Any(tile => tile != '#'))
        {
            throw new ArgumentException("The top and bottom map boundaries must be solid.", nameof(TileRows));
        }

        for (var row = 0; row < GridHeight; row++)
        {
            if (TileRows[row][0] != '#' || TileRows[row][^1] != '#')
            {
                throw new ArgumentException("The left and right map boundaries must be solid.", nameof(TileRows));
            }
        }
    }

    private void ValidateEntities()
    {
        if (FolderPositions.Length != 3)
        {
            throw new ArgumentException("A map must contain exactly three folders.", nameof(FolderPositions));
        }

        if (Guards.Length != 2)
        {
            throw new ArgumentException("A map must contain exactly two guards.", nameof(Guards));
        }

        if (IsSolid(PlayerSpawn, GameSession.PlayerRadius))
        {
            throw new ArgumentException("The player spawn must be on walkable ground.", nameof(PlayerSpawn));
        }

        if (IsSolid(ExitZone.Center))
        {
            throw new ArgumentException("The exit must be on walkable ground.", nameof(ExitZone));
        }

        if (FolderPositions.Any(position => IsSolid(position)))
        {
            throw new ArgumentException("Every folder must be on walkable ground.", nameof(FolderPositions));
        }

        foreach (var guard in Guards)
        {
            if (guard.Waypoints.Any(position => IsSolid(position)))
            {
                throw new ArgumentException("Every guard waypoint must be on walkable ground.", nameof(Guards));
            }
        }
    }
}
