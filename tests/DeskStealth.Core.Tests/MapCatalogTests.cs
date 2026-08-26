using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

public sealed class MapCatalogTests
{
    [Fact]
    public void CatalogContainsThreeClosedFixedSizeMapsWithRequiredEntities()
    {
        Assert.Equal(3, MapCatalog.All.Length);
        Assert.Equal(3, MapCatalog.All.Select(map => map.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var map in MapCatalog.All)
        {
            Assert.Equal(MapDefinition.GridHeight, map.TileRows.Length);
            Assert.All(map.TileRows, row => Assert.Equal(MapDefinition.GridWidth, row.Length));
            Assert.All(map.TileRows[0], tile => Assert.Equal('#', tile));
            Assert.All(map.TileRows[^1], tile => Assert.Equal('#', tile));
            Assert.All(map.TileRows, row =>
            {
                Assert.Equal('#', row[0]);
                Assert.Equal('#', row[^1]);
            });
            Assert.Equal(3, map.FolderPositions.Length);
            Assert.Equal(2, map.Guards.Length);
            Assert.False(map.IsSolid(map.PlayerSpawn, GameSession.PlayerRadius));
            Assert.False(map.IsSolid(map.ExitZone.Center));
            Assert.All(map.FolderPositions, folder => Assert.False(map.IsSolid(folder)));
            Assert.All(map.Guards, guard =>
                Assert.All(guard.Waypoints, waypoint => Assert.False(map.IsSolid(waypoint))));
        }
    }

    [Fact]
    public void AllObjectivesAndPatrolWaypointsAreReachableFromSpawn()
    {
        foreach (var map in MapCatalog.All)
        {
            var reachable = ReachableCells(map, CellOf(map.PlayerSpawn));
            Assert.Contains(CellOf(map.ExitZone.Center), reachable);
            Assert.All(map.FolderPositions, folder => Assert.Contains(CellOf(folder), reachable));
            Assert.All(map.Guards, guard =>
                Assert.All(guard.Waypoints, waypoint => Assert.Contains(CellOf(waypoint), reachable)));
        }
    }

    [Fact]
    public void PureWalkingObjectiveRoutesTakeBetweenTwentyEightAndThirtySixSeconds()
    {
        foreach (var map in MapCatalog.All)
        {
            var distance = ShortestObjectiveRouteDistance(map);
            var seconds = distance / GameSession.WalkingSpeed;

            Assert.InRange(seconds, 28d, 36d);
            Assert.True(seconds <= 50d, $"{map.Id} has no sub-50-second geometric route.");
        }
    }

    [Fact]
    public void GuardTimedSafeRoutesCompleteWithinFiftySeconds()
    {
        foreach (var map in MapCatalog.All)
        {
            var safeRouteSeconds = EarliestGuardSafeRouteSeconds(map, maximumSeconds: 50d);
            Assert.True(
                safeRouteSeconds.HasValue,
                $"{map.Id} has no guard-safe route that collects every folder and reaches the exit within 50 seconds.");
            Assert.InRange(safeRouteSeconds.Value, 0d, 50d);
        }
    }

    [Fact]
    public void PlayerDoesNotStartInsideInitialGuardView()
    {
        foreach (var map in MapCatalog.All)
        {
            var session = new GameSession(map);
            var snapshot = session.Step(InputFrame.None, 0.001);
            Assert.All(snapshot.Guards, guard => Assert.False(guard.SeesPlayer));

            foreach (var guard in map.Guards)
            {
                Assert.False(IsInInitialView(map, guard, map.PlayerSpawn));
                Assert.All(map.FolderPositions, folder =>
                    Assert.False(IsInInitialView(map, guard, folder)));
            }
        }
    }

    [Fact]
    public void MandatoryAisleCrossingsProvideAtLeastOnePointTwoSecondSafeWindow()
    {
        const double sampleSeconds = 1d / 30d;
        foreach (var map in MapCatalog.All)
        {
            var crossings = MandatoryVerticalCrossings(map).ToArray();
            Assert.NotEmpty(crossings);

            foreach (var crossing in crossings)
            {
                var guards = map.Guards.Select(definition => new SimulatedGuard(definition)).ToArray();
                var longestSafeWindow = 0d;
                var currentSafeWindow = 0d;
                for (var elapsed = 0d; elapsed < GameSession.RoundDurationSeconds; elapsed += sampleSeconds)
                {
                    var visible = guards.Any(guard => IsInView(map, guard.Position, guard.FacingDegrees, crossing));
                    if (visible)
                    {
                        currentSafeWindow = 0d;
                    }
                    else
                    {
                        currentSafeWindow += sampleSeconds;
                        longestSafeWindow = Math.Max(longestSafeWindow, currentSafeWindow);
                    }

                    foreach (var guard in guards)
                    {
                        guard.Step(sampleSeconds);
                    }
                }

                Assert.True(
                    longestSafeWindow >= 1.2d,
                    $"{map.Id} crossing {crossing} has only {longestSafeWindow:F2}s of continuous safety.");
            }
        }
    }

    [Fact]
    public void CatalogOrderWrapsAndUnknownMapsRetryThemselves()
    {
        Assert.Same(MapCatalog.All[1], MapCatalog.Next(MapCatalog.All[0]));
        Assert.Same(MapCatalog.All[2], MapCatalog.Next(MapCatalog.All[1]));
        Assert.Same(MapCatalog.All[0], MapCatalog.Next(MapCatalog.All[2]));

        var custom = TestMaps.Open(id: "not-in-catalog");
        Assert.Same(custom, MapCatalog.Next(custom));
    }

    private static double ShortestObjectiveRouteDistance(MapDefinition map)
    {
        var start = CellOf(map.PlayerSpawn);
        var exit = CellOf(map.ExitZone.Center);
        var folders = map.FolderPositions.Select(CellOf).ToArray();
        var best = double.PositiveInfinity;

        foreach (var order in Permutations(folders))
        {
            var total = 0d;
            var current = start;
            foreach (var objective in order.Append(exit))
            {
                total += ShortestDistance(map, current, objective);
                current = objective;
            }

            best = Math.Min(best, total);
        }

        return best;
    }

    private static IEnumerable<(int Column, int Row)[]> Permutations((int Column, int Row)[] cells)
    {
        for (var first = 0; first < cells.Length; first++)
        {
            for (var second = 0; second < cells.Length; second++)
            {
                if (second == first)
                {
                    continue;
                }

                for (var third = 0; third < cells.Length; third++)
                {
                    if (third != first && third != second)
                    {
                        yield return [cells[first], cells[second], cells[third]];
                    }
                }
            }
        }
    }

    private static double ShortestDistance(
        MapDefinition map,
        (int Column, int Row) start,
        (int Column, int Row) target)
    {
        var distances = new Dictionary<(int Column, int Row), double> { [start] = 0 };
        var queue = new PriorityQueue<(int Column, int Row), double>();
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var current, out var currentDistance))
        {
            if (currentDistance > distances[current] + 0.000001)
            {
                continue;
            }

            if (current == target)
            {
                return currentDistance;
            }

            foreach (var (next, cost) in WalkingNeighbors(map, current))
            {
                var candidateDistance = currentDistance + cost;
                if (distances.TryGetValue(next, out var knownDistance) &&
                    candidateDistance >= knownDistance - 0.000001)
                {
                    continue;
                }

                distances[next] = candidateDistance;
                queue.Enqueue(next, candidateDistance);
            }
        }

        throw new InvalidOperationException($"No route from {start} to {target} in {map.Id}.");
    }

    private static double? EarliestGuardSafeRouteSeconds(MapDefinition map, double maximumSeconds)
    {
        const double sampleSeconds = 1d / 30d;
        var frameLimit = (int)Math.Floor(maximumSeconds / sampleSeconds);
        var guards = map.Guards.Select(definition => new SimulatedGuard(definition)).ToArray();
        var patrol = new GuardPose[frameLimit + 1][];
        for (var frame = 0; frame <= frameLimit; frame++)
        {
            patrol[frame] = guards.Select(guard => new GuardPose(guard.Position, guard.FacingDegrees)).ToArray();
            foreach (var guard in guards)
            {
                guard.Step(sampleSeconds);
            }
        }

        var cells = Enumerable.Range(0, MapDefinition.GridHeight)
            .SelectMany(row => Enumerable.Range(0, MapDefinition.GridWidth).Select(column => (Column: column, Row: row)))
            .Where(cell => map.TileAt(cell.Column, cell.Row) != '#')
            .ToArray();
        var cellIndices = cells
            .Select((cell, index) => (Cell: cell, Index: index))
            .ToDictionary(item => item.Cell, item => item.Index);
        var start = CellOf(map.PlayerSpawn);
        var exit = CellOf(map.ExitZone.Center);
        var folderMaskByCell = new Dictionary<(int Column, int Row), int>();
        for (var index = 0; index < map.FolderPositions.Length; index++)
        {
            var cell = CellOf(map.FolderPositions[index]);
            folderMaskByCell[cell] = folderMaskByCell.GetValueOrDefault(cell) | (1 << index);
        }

        var visited = new bool[frameLimit + 1, cells.Length * 8];
        var queue = new PriorityQueue<RouteNode, int>();
        Enqueue(0, cellIndices[start], folderMaskByCell.GetValueOrDefault(start));

        while (queue.TryDequeue(out var node, out _))
        {
            var cell = cells[node.CellIndex];
            if (node.FolderMask == 7 && cell == exit)
            {
                return node.Frame * sampleSeconds;
            }

            if (node.Frame >= frameLimit)
            {
                continue;
            }

            var cellCenter = MapDefinition.CellCenter(cell.Column, cell.Row);
            var canWait = map.TileAt(cell.Column, cell.Row) == 'C' ||
                          IsSafeAt(map, patrol[node.Frame + 1], cellCenter);
            if (canWait)
            {
                Enqueue(node.Frame + 1, node.CellIndex, node.FolderMask);
            }

            foreach (var (next, distance) in WalkingNeighbors(map, cell))
            {
                var travelFrames = (int)Math.Ceiling(distance / (GameSession.WalkingSpeed * sampleSeconds));
                var arrivalFrame = node.Frame + travelFrames;
                if (arrivalFrame > frameLimit)
                {
                    continue;
                }

                var destination = MapDefinition.CellCenter(next.Column, next.Row);
                var safe = true;
                for (var travelFrame = 1; travelFrame <= travelFrames; travelFrame++)
                {
                    var progress = travelFrame / (double)travelFrames;
                    var position = new WorldPoint(
                        cellCenter.X + ((destination.X - cellCenter.X) * progress),
                        cellCenter.Y + ((destination.Y - cellCenter.Y) * progress));
                    if (!IsSafeAt(map, patrol[node.Frame + travelFrame], position))
                    {
                        safe = false;
                        break;
                    }
                }

                if (!safe)
                {
                    continue;
                }

                var folderMask = node.FolderMask | folderMaskByCell.GetValueOrDefault(next);
                Enqueue(arrivalFrame, cellIndices[next], folderMask);
            }
        }

        return null;

        void Enqueue(int frame, int cellIndex, int folderMask)
        {
            var stateIndex = (cellIndex * 8) + folderMask;
            if (visited[frame, stateIndex])
            {
                return;
            }

            visited[frame, stateIndex] = true;
            queue.Enqueue(new RouteNode(frame, cellIndex, folderMask), frame);
        }
    }

    private static IEnumerable<((int Column, int Row) Cell, double Cost)> WalkingNeighbors(
        MapDefinition map,
        (int Column, int Row) cell)
    {
        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                if (columnOffset == 0 && rowOffset == 0)
                {
                    continue;
                }

                var next = (Column: cell.Column + columnOffset, Row: cell.Row + rowOffset);
                if (next.Column is < 0 or >= MapDefinition.GridWidth ||
                    next.Row is < 0 or >= MapDefinition.GridHeight ||
                    map.TileAt(next.Column, next.Row) == '#')
                {
                    continue;
                }

                if (columnOffset != 0 && rowOffset != 0 &&
                    (map.TileAt(cell.Column + columnOffset, cell.Row) == '#' ||
                     map.TileAt(cell.Column, cell.Row + rowOffset) == '#'))
                {
                    continue;
                }

                var cost = MapDefinition.CellSize * Math.Sqrt((columnOffset * columnOffset) + (rowOffset * rowOffset));
                yield return (next, cost);
            }
        }
    }

    private static bool IsSafeAt(MapDefinition map, GuardPose[] guards, WorldPoint position) =>
        guards.All(guard => !IsInView(map, guard.Position, guard.FacingDegrees, position));

    private static HashSet<(int Column, int Row)> ReachableCells(
        MapDefinition map,
        (int Column, int Row) start)
    {
        var visited = new HashSet<(int Column, int Row)> { start };
        var queue = new Queue<(int Column, int Row)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in Neighbors(current))
            {
                if (next.Column is < 0 or >= MapDefinition.GridWidth ||
                    next.Row is < 0 or >= MapDefinition.GridHeight ||
                    map.TileAt(next.Column, next.Row) == '#' ||
                    !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return visited;
    }

    private static IEnumerable<(int Column, int Row)> Neighbors((int Column, int Row) cell)
    {
        yield return (cell.Column + 1, cell.Row);
        yield return (cell.Column - 1, cell.Row);
        yield return (cell.Column, cell.Row + 1);
        yield return (cell.Column, cell.Row - 1);
    }

    private static (int Column, int Row) CellOf(WorldPoint point) =>
        ((int)(point.X / MapDefinition.CellSize),
         (int)((point.Y - MapDefinition.HudHeight) / MapDefinition.CellSize));

    private static bool IsInInitialView(MapDefinition map, GuardDefinition guard, WorldPoint target)
    {
        var offset = target - guard.Spawn;
        var distance = offset.Length;
        if (distance > GameSession.GuardViewRange)
        {
            return false;
        }

        if (distance <= 0.000001)
        {
            return true;
        }

        var facing = WorldPoint.FromAngleDegrees(guard.InitialFacingDegrees);
        var cosine = WorldPoint.Dot(facing, offset / distance);
        var minimumCosine = Math.Cos((GameSession.GuardViewAngleDegrees / 2d) * Math.PI / 180d);
        return cosine >= minimumCosine && map.HasClearLine(guard.Spawn, target);
    }

    private static IEnumerable<WorldPoint> MandatoryVerticalCrossings(MapDefinition map)
    {
        for (var row = 1; row < MapDefinition.GridHeight - 1; row++)
        {
            for (var column = 1; column < MapDefinition.GridWidth - 1; column++)
            {
                if (map.TileAt(column, row) != '#' &&
                    map.TileAt(column - 1, row) == '#' &&
                    map.TileAt(column + 1, row) == '#' &&
                    map.TileAt(column, row - 1) != '#' &&
                    map.TileAt(column, row + 1) != '#')
                {
                    yield return MapDefinition.CellCenter(column, row);
                }
            }
        }
    }

    private static bool IsInView(
        MapDefinition map,
        WorldPoint guardPosition,
        double facingDegrees,
        WorldPoint target)
    {
        var offset = target - guardPosition;
        var distance = offset.Length;
        if (distance > GameSession.GuardViewRange)
        {
            return false;
        }

        if (distance <= 0.000001)
        {
            return true;
        }

        var facing = WorldPoint.FromAngleDegrees(facingDegrees);
        var minimumCosine = Math.Cos((GameSession.GuardViewAngleDegrees / 2d) * Math.PI / 180d);
        return WorldPoint.Dot(facing, offset / distance) >= minimumCosine &&
               map.HasClearLine(guardPosition, target);
    }

    private sealed class SimulatedGuard
    {
        private readonly GuardDefinition _definition;
        private int _targetWaypointIndex = 1;
        private double _pauseRemaining;

        public SimulatedGuard(GuardDefinition definition)
        {
            _definition = definition;
            Position = definition.Spawn;
            FacingDegrees = definition.InitialFacingDegrees;
        }

        public WorldPoint Position { get; private set; }

        public double FacingDegrees { get; private set; }

        public void Step(double deltaSeconds)
        {
            if (_pauseRemaining > 0)
            {
                _pauseRemaining = Math.Max(0, _pauseRemaining - deltaSeconds);
                return;
            }

            var target = _definition.Waypoints[_targetWaypointIndex];
            var offset = target - Position;
            var distance = offset.Length;
            if (distance <= 0.000001)
            {
                Arrive();
                return;
            }

            var desiredFacing = Math.Atan2(offset.Y, offset.X) * 180d / Math.PI;
            var turn = ((desiredFacing - FacingDegrees + 540d) % 360d) - 180d;
            var maximumTurn = GameSession.GuardMaximumTurnDegreesPerSecond * deltaSeconds;
            FacingDegrees = (FacingDegrees + Math.Clamp(turn, -maximumTurn, maximumTurn) + 360d) % 360d;

            var travel = GameSession.GuardSpeed * deltaSeconds;
            if (travel >= distance - 0.000001)
            {
                Position = target;
                Arrive();
            }
            else
            {
                Position += (offset / distance) * travel;
            }
        }

        private void Arrive()
        {
            _pauseRemaining = GameSession.GuardWaypointPauseSeconds;
            _targetWaypointIndex = (_targetWaypointIndex + 1) % _definition.Waypoints.Length;
        }
    }

    private readonly record struct GuardPose(WorldPoint Position, double FacingDegrees);

    private readonly record struct RouteNode(int Frame, int CellIndex, int FolderMask);
}
