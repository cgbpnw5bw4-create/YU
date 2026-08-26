using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

public sealed class GameSessionFlowTests
{
    [Fact]
    public void ClockMode_FreezesSimulationAndLeaveClockUsesReadyGate()
    {
        var session = new GameSession(TestMaps.Open());
        var beforeClock = session.Snapshot;

        session.EnterClock();
        var frozen = session.Step(new InputFrame(D: true, Shift: true), 10d);

        Assert.Equal(AppMode.Clock, frozen.Mode);
        Assert.Equal(beforeClock.PlayerPosition, frozen.PlayerPosition);
        Assert.Equal(beforeClock.RemainingSeconds, frozen.RemainingSeconds);
        Assert.Equal(beforeClock.Guards[0].Position, frozen.Guards[0].Position);

        var ready = session.LeaveClock();
        Assert.Equal(AppMode.Ready, ready.Mode);
        Assert.Equal(GameSession.ReadyGateSeconds, ready.ReadyRemainingSeconds);

        var halfReady = session.Step(new InputFrame(D: true), 0.5);
        Assert.Equal(AppMode.Ready, halfReady.Mode);
        Assert.Equal(beforeClock.PlayerPosition, halfReady.PlayerPosition);

        var gateComplete = session.Step(new InputFrame(D: true), 0.5);
        Assert.Equal(AppMode.Playing, gateComplete.Mode);
        Assert.Equal(beforeClock.PlayerPosition, gateComplete.PlayerPosition);

        var resumed = session.Step(new InputFrame(D: true), 0.1);
        Assert.True(resumed.PlayerPosition.X > beforeClock.PlayerPosition.X);
    }

    [Fact]
    public void ReadyGate_WaitsForFirstMovementAfterCountdown()
    {
        var session = new GameSession(TestMaps.Open(), AppMode.Ready);

        var elapsed = session.Step(InputFrame.None, 2d);
        Assert.Equal(AppMode.Ready, elapsed.Mode);
        Assert.Equal(0d, elapsed.ReadyRemainingSeconds);
        Assert.Equal(GameSession.RoundDurationSeconds, elapsed.RemainingSeconds);

        var moving = session.Step(new InputFrame(A: true), 0.1);
        Assert.Equal(AppMode.Playing, moving.Mode);
        Assert.Equal(GameSession.RoundDurationSeconds - 0.1, moving.RemainingSeconds, 5);
    }

    [Fact]
    public void SessionCanStartInClockAndLeaveIntoReadyGate()
    {
        var session = new GameSession(TestMaps.Open(), AppMode.Clock);

        Assert.Equal(AppMode.Clock, session.Snapshot.Mode);
        var ready = session.LeaveClock();

        Assert.Equal(AppMode.Ready, ready.Mode);
        Assert.Equal(GameSession.ReadyGateSeconds, ready.ReadyRemainingSeconds);
    }

    [Fact]
    public void Timeout_LosesAtSixtySeconds()
    {
        var session = new GameSession(TestMaps.Open());

        var lost = session.Step(InputFrame.None, GameSession.RoundDurationSeconds);

        Assert.Equal(AppMode.Lost, lost.Mode);
        Assert.Equal(0d, lost.RemainingSeconds, 5);
        Assert.Equal(0, lost.Score);
    }

    [Fact]
    public void ExitRemainsLockedUntilAllThreeFoldersAreCollected()
    {
        var spawn = TestMaps.P(5, 4);
        var map = TestMaps.Open(
            spawn: spawn,
            exit: new WorldRect(spawn.X - 6, spawn.Y - 6, 12, 12));
        var session = new GameSession(map);

        var snapshot = session.Step(InputFrame.None, 0.1);

        Assert.False(snapshot.ExitUnlocked);
        Assert.Equal(AppMode.Playing, snapshot.Mode);
    }

    [Fact]
    public void FoldersAutoCollectAndUnlockedExitWinsWithScoreAndBonus()
    {
        var spawn = TestMaps.P(5, 4);
        var map = TestMaps.Open(
            id: MapCatalog.First.Id,
            spawn: spawn,
            exit: TestMaps.InnerCell(6, 4),
            folders: [spawn, spawn, spawn]);
        var session = new GameSession(map);

        var won = session.Step(new InputFrame(D: true), 0.3);

        Assert.Equal(AppMode.Won, won.Mode);
        Assert.True(won.ExitUnlocked);
        Assert.Equal(3, won.CollectedFolderCount);
        Assert.Equal(((int)Math.Floor(won.RemainingSeconds) * 100) + 500, won.Score);
        Assert.Equal(MapCatalog.All[1].Id, won.ContinuationMapId);
        Assert.Same(MapCatalog.All[1], session.ContinuationMap);
    }

    [Fact]
    public void AWinAfterCatch_DoesNotReceiveNoCatchBonus()
    {
        var spawn = TestMaps.P(5, 4);
        var session = new GameSession(TestMaps.Watched(
            folders: [spawn, spawn, spawn],
            exit: TestMaps.InnerCell(6, 4)));
        session.Step(InputFrame.None, GameSession.WalkingDetectionSeconds);
        session.Step(InputFrame.None, GameSession.CatchFreezeSeconds + GameSession.CatchGraceSeconds);

        var won = session.Step(new InputFrame(D: true), 0.3);

        Assert.Equal(AppMode.Won, won.Mode);
        Assert.Equal((int)Math.Floor(won.RemainingSeconds) * 100, won.Score);
    }

    [Fact]
    public void Restart_RetriesSameMapAndResetsRoundState()
    {
        var session = new GameSession(TestMaps.Open());
        session.Step(InputFrame.None, GameSession.RoundDurationSeconds);

        var restarted = session.Restart();

        Assert.Equal(AppMode.Ready, restarted.Mode);
        Assert.Equal(session.Map.Id, restarted.MapId);
        Assert.Equal(session.Map.Id, restarted.ContinuationMapId);
        Assert.Equal(GameSession.RoundDurationSeconds, restarted.RemainingSeconds);
        Assert.Equal(0, restarted.CatchCount);
        Assert.Equal(0, restarted.CollectedFolderCount);
        Assert.Equal(0, restarted.Score);
    }

    [Fact]
    public void Snapshot_DoesNotAdvanceState()
    {
        var session = new GameSession(TestMaps.Open());

        var first = session.Snapshot;
        var second = session.Snapshot;

        Assert.Equal(first.Mode, second.Mode);
        Assert.Equal(first.PlayerPosition, second.PlayerPosition);
        Assert.Equal(first.RemainingSeconds, second.RemainingSeconds);
        Assert.Equal(first.StaminaFraction, second.StaminaFraction);
        Assert.Equal(first.DetectionFraction, second.DetectionFraction);
        Assert.Equal(first.Guards.ToArray(), second.Guards.ToArray());
        Assert.Equal(first.Folders.ToArray(), second.Folders.ToArray());
    }
}
