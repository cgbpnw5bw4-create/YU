using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

public sealed class GuardPatrolTests
{
    [Fact]
    public void GuardPatrol_UsesSpecifiedSpeedAndMaximumTurnRate()
    {
        var start = TestMaps.P(3, 4);
        var target = TestMaps.P(8, 4);
        var guards = new[]
        {
            new GuardDefinition([start, target], 90),
            TestMaps.StationaryGuard(TestMaps.P(13, 1), 0),
        };
        var session = new GameSession(TestMaps.Open(spawn: TestMaps.P(13, 7), guards: guards));

        var snapshot = session.Step(InputFrame.None, 0.1);

        Assert.Equal(GameSession.GuardSpeed * 0.1, snapshot.Guards[0].Position.X - start.X, 5);
        Assert.Equal(72d, snapshot.Guards[0].FacingDegrees, 5);
    }

    [Fact]
    public void GuardPatrol_PausesAtWaypointBeforeReturning()
    {
        var start = TestMaps.P(3, 4);
        var target = TestMaps.P(4, 4);
        var guards = new[]
        {
            new GuardDefinition([start, target], 0),
            TestMaps.StationaryGuard(TestMaps.P(13, 1), 0),
        };
        var session = new GameSession(TestMaps.Open(spawn: TestMaps.P(13, 7), guards: guards));

        var arrival = session.Step(InputFrame.None, 16d / GameSession.GuardSpeed);
        Assert.Equal(target.X, arrival.Guards[0].Position.X, 5);
        Assert.Equal(target.Y, arrival.Guards[0].Position.Y, 5);
        Assert.Equal(GameSession.GuardWaypointPauseSeconds, arrival.Guards[0].PauseRemainingSeconds, 4);

        var paused = session.Step(InputFrame.None, 0.2);
        Assert.Equal(target, paused.Guards[0].Position);
        Assert.Equal(0.2d, paused.Guards[0].PauseRemainingSeconds, 4);
        Assert.Equal(0, paused.Guards[0].TargetWaypointIndex);
    }
}
