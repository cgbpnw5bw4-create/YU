using System.Collections.Immutable;

namespace DeskStealth.Core;

/// <summary>The three deterministic, hand-authored office maps in campaign order.</summary>
public static class MapCatalog
{
    public static ImmutableArray<MapDefinition> All { get; } =
    [
        CreateBlueprintOffice(),
        CreateRecordsRoom(),
        CreateNightShift(),
    ];

    public static MapDefinition First => All[0];

    public static MapDefinition Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return All.FirstOrDefault(map => string.Equals(map.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"No DeskStealth map has the id '{id}'.");
    }

    public static MapDefinition Next(MapDefinition current)
    {
        ArgumentNullException.ThrowIfNull(current);
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Id, current.Id, StringComparison.Ordinal))
            {
                return All[(index + 1) % All.Length];
            }
        }

        return current;
    }

    private static MapDefinition CreateBlueprintOffice() => new(
        id: "blueprint-office",
        name: "冷色工位蓝图",
        tileRows:
        [
            "################",
            "#...C..........#",
            "##############.#",
            "#......C.......#",
            "#.##############",
            "#..........C...#",
            "##############.#",
            "#..............#",
            "################",
        ],
        playerSpawn: P(1, 1),
        exitZone: Exit(11, 1),
        folderPositions: [P(10, 3), P(9, 5), P(1, 7)],
        guards:
        [
            Guard(180, P(7, 3), P(2, 3)),
            Guard(180, P(8, 7), P(13, 7)),
        ]);

    private static MapDefinition CreateRecordsRoom() => new(
        id: "records-room",
        name: "档案回廊",
        tileRows:
        [
            "################",
            "#..........C...#",
            "#.##############",
            "#....C.........#",
            "##############.#",
            "#.........C....#",
            "#.##############",
            "#..............#",
            "################",
        ],
        playerSpawn: P(14, 1),
        exitZone: Exit(4, 1),
        folderPositions: [P(5, 3), P(10, 5), P(14, 7)],
        guards:
        [
            Guard(0, P(8, 3), P(13, 3)),
            Guard(0, P(7, 7), P(2, 7)),
        ]);

    private static MapDefinition CreateNightShift() => new(
        id: "night-shift",
        name: "夜班隔间",
        tileRows:
        [
            "################",
            "#......C.......#",
            "##############.#",
            "#..........C...#",
            "#.##############",
            "#...C..........#",
            "##############.#",
            "#..............#",
            "################",
        ],
        playerSpawn: P(1, 1),
        exitZone: Exit(11, 1),
        folderPositions: [P(11, 3), P(4, 5), P(1, 7)],
        guards:
        [
            Guard(0, P(6, 3), P(2, 3)),
            Guard(180, P(9, 7), P(13, 7)),
        ]);

    private static WorldPoint P(int column, int row) => MapDefinition.CellCenter(column, row);

    private static WorldRect Exit(int column, int row)
    {
        var cell = MapDefinition.CellBounds(column, row);
        return new WorldRect(cell.X + 2, cell.Y + 2, cell.Width - 4, cell.Height - 4);
    }

    private static GuardDefinition Guard(
        double initialFacingDegrees,
        params WorldPoint[] waypoints) => new(waypoints, initialFacingDegrees);
}
