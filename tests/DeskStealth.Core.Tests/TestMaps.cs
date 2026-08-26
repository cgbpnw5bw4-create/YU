using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

internal static class TestMaps
{
    private static readonly string[] OpenRows =
    [
        "################",
        "#..............#",
        "#..............#",
        "#..............#",
        "#..............#",
        "#..............#",
        "#..............#",
        "#..............#",
        "################",
    ];

    public static MapDefinition Open(
        string id = "open-test",
        WorldPoint? spawn = null,
        WorldRect? exit = null,
        IEnumerable<WorldPoint>? folders = null,
        IEnumerable<GuardDefinition>? guards = null,
        IEnumerable<string>? rows = null)
    {
        var playerSpawn = spawn ?? P(5, 4);
        return new MapDefinition(
            id,
            "Open test room",
            rows ?? OpenRows,
            playerSpawn,
            exit ?? InnerCell(12, 7),
            folders ?? [P(2, 1), P(13, 1), P(2, 7)],
            guards ?? SafeGuards());
    }

    public static MapDefinition Watched(
        bool coverAtSpawn = false,
        bool wallBetweenGuardAndPlayer = false,
        bool wallAbovePlayer = false,
        IEnumerable<WorldPoint>? folders = null,
        WorldRect? exit = null,
        string id = "watched-test")
    {
        var rows = OpenRows.ToArray();
        var row4 = rows[4].ToCharArray();
        if (coverAtSpawn)
        {
            row4[5] = 'C';
        }

        if (wallBetweenGuardAndPlayer)
        {
            row4[4] = '#';
        }

        rows[4] = new string(row4);
        if (wallAbovePlayer)
        {
            var row3 = rows[3].ToCharArray();
            row3[5] = '#';
            rows[3] = new string(row3);
        }

        var spawn = P(5, 4);
        return Open(
            id,
            spawn,
            exit ?? InnerCell(6, 4),
            folders,
            [StationaryGuard(P(3, 4), 0), StationaryGuard(P(13, 7), 0)],
            rows);
    }

    public static WorldPoint P(int column, int row) => MapDefinition.CellCenter(column, row);

    public static WorldRect InnerCell(int column, int row)
    {
        var bounds = MapDefinition.CellBounds(column, row);
        return new WorldRect(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
    }

    public static GuardDefinition StationaryGuard(WorldPoint position, double facingDegrees) =>
        new([position, position], facingDegrees);

    private static IEnumerable<GuardDefinition> SafeGuards() =>
    [
        StationaryGuard(P(13, 1), 0),
        StationaryGuard(P(13, 7), 0),
    ];
}
