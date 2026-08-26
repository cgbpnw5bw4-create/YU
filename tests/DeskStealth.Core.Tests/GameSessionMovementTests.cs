using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

public sealed class GameSessionMovementTests
{
    [Fact]
    public void DiagonalMovement_IsNormalizedToWalkingSpeed()
    {
        var map = TestMaps.Open();
        var horizontal = new GameSession(map);
        var diagonal = new GameSession(map);

        var horizontalStart = horizontal.Snapshot.PlayerPosition;
        var diagonalStart = diagonal.Snapshot.PlayerPosition;
        var horizontalEnd = horizontal.Step(new InputFrame(D: true), 0.25).PlayerPosition;
        var diagonalEnd = diagonal.Step(new InputFrame(W: true, D: true), 0.25).PlayerPosition;

        Assert.Equal(12d, WorldPoint.Distance(horizontalStart, horizontalEnd), 6);
        Assert.Equal(12d, WorldPoint.Distance(diagonalStart, diagonalEnd), 6);
        Assert.Equal(horizontal.Snapshot.RemainingSeconds, diagonal.Snapshot.RemainingSeconds, 6);
    }

    [Theory]
    [InlineData(false, false, 12d, MovementMode.Walking)]
    [InlineData(true, false, 19d, MovementMode.Sprinting)]
    [InlineData(false, true, 7d, MovementMode.Crouching)]
    [InlineData(true, true, 7d, MovementMode.Crouching)]
    public void Movement_UsesSpecifiedSpeedAndCtrlPriority(
        bool shift,
        bool ctrl,
        double expectedDistance,
        MovementMode expectedMode)
    {
        var session = new GameSession(TestMaps.Open());
        var start = session.Snapshot.PlayerPosition;

        var snapshot = session.Step(new InputFrame(D: true, Shift: shift, Ctrl: ctrl), 0.25);

        Assert.Equal(expectedDistance, WorldPoint.Distance(start, snapshot.PlayerPosition), 6);
        Assert.Equal(expectedMode, snapshot.MovementMode);
        if (ctrl)
        {
            Assert.Equal(1d, snapshot.StaminaFraction, 6);
        }
    }

    [Fact]
    public void Sprint_ConsumesTwoSecondsThenRechargesAfterDelay()
    {
        var session = new GameSession(TestMaps.Open(spawn: TestMaps.P(2, 4)));

        var exhausted = session.Step(new InputFrame(D: true, Shift: true), 2d);
        Assert.Equal(0d, exhausted.StaminaFraction, 6);
        Assert.Equal(GameSession.SprintRechargeDelaySeconds, exhausted.SprintRechargeDelayRemainingSeconds, 6);

        var beforeDelay = session.Step(InputFrame.None, 0.74);
        Assert.Equal(0d, beforeDelay.StaminaFraction, 6);

        var afterDelay = session.Step(InputFrame.None, 0.01);
        Assert.Equal(0d, afterDelay.StaminaFraction, 5);

        var half = session.Step(InputFrame.None, 1.25);
        Assert.Equal(0.5d, half.StaminaFraction, 5);

        var full = session.Step(InputFrame.None, 1.25);
        Assert.Equal(1d, full.StaminaFraction, 5);
    }

    [Fact]
    public void HoldingExhaustedSprint_DoesNotRechargeUntilReleased()
    {
        var session = new GameSession(TestMaps.Open(spawn: TestMaps.P(2, 4)));
        session.Step(new InputFrame(D: true, Shift: true), 2d);

        var held = session.Step(new InputFrame(W: true, Shift: true), 1d);

        Assert.Equal(0d, held.StaminaFraction, 6);
        Assert.Equal(MovementMode.Walking, held.MovementMode);
    }

    [Fact]
    public void Player_CannotMoveThroughSolidObstacle()
    {
        var session = new GameSession(TestMaps.Watched(
            wallBetweenGuardAndPlayer: true,
            wallAbovePlayer: true));
        var start = session.Snapshot.PlayerPosition;

        var snapshot = session.Step(new InputFrame(W: true), 1d);

        Assert.True(snapshot.PlayerPosition.Y < start.Y);
        Assert.True(snapshot.PlayerPosition.Y >= MapDefinition.CellBounds(5, 3).Bottom + GameSession.PlayerRadius - 0.001);
    }
}
