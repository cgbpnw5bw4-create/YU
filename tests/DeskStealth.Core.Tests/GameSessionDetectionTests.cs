using DeskStealth.Core;

namespace DeskStealth.Core.Tests;

public sealed class GameSessionDetectionTests
{
    [Fact]
    public void SolidObstacle_OccludesGuardView()
    {
        var clear = new GameSession(TestMaps.Watched());
        var occluded = new GameSession(TestMaps.Watched(wallBetweenGuardAndPlayer: true));

        var clearSnapshot = clear.Step(InputFrame.None, 0.1);
        var occludedSnapshot = occluded.Step(InputFrame.None, 0.1);

        Assert.True(clearSnapshot.Guards[0].SeesPlayer);
        Assert.True(clearSnapshot.DetectionFraction > 0);
        Assert.False(occludedSnapshot.Guards[0].SeesPlayer);
        Assert.Equal(0d, occludedSnapshot.DetectionFraction, 6);
    }

    [Fact]
    public void CrouchingInCover_CompletelyHidesPlayerAndCtrlOverridesSprint()
    {
        var session = new GameSession(TestMaps.Watched(coverAtSpawn: true));

        var snapshot = session.Step(new InputFrame(D: true, Shift: true, Ctrl: true), 0.1);

        Assert.Equal(MovementMode.Crouching, snapshot.MovementMode);
        Assert.True(snapshot.IsHidden);
        Assert.False(snapshot.Guards[0].SeesPlayer);
        Assert.Equal(0d, snapshot.DetectionFraction, 6);
        Assert.Equal(1d, snapshot.StaminaFraction, 6);
        Assert.Equal(GameSession.CrouchingSpeed * 0.1, snapshot.PlayerPosition.X - TestMaps.P(5, 4).X, 5);
    }

    [Fact]
    public void CrouchingOutsideCover_IsStillVisibleWithOneSecondThreshold()
    {
        var session = new GameSession(TestMaps.Watched(wallAbovePlayer: true));

        var partial = session.Step(new InputFrame(W: true, Ctrl: true), 0.5);
        Assert.Equal(0, partial.CatchCount);
        Assert.Equal(0.5d, partial.DetectionFraction, 5);

        var caught = session.Step(new InputFrame(W: true, Ctrl: true), 0.5);
        Assert.Equal(1, caught.CatchCount);
    }

    [Theory]
    [InlineData(false, 0.75)]
    [InlineData(true, 0.5)]
    public void Exposure_UsesWalkingAndSprintCatchThresholds(bool sprint, double threshold)
    {
        var session = new GameSession(TestMaps.Watched(wallAbovePlayer: true));
        var input = new InputFrame(W: true, Shift: sprint);

        var before = session.Step(input, threshold - 0.02);
        Assert.Equal(0, before.CatchCount);

        var caught = session.Step(input, 0.02);
        Assert.Equal(1, caught.CatchCount);
    }

    [Fact]
    public void Detection_DecaysLinearlyToZeroInOneSecondOutOfSight()
    {
        var session = new GameSession(TestMaps.Watched(coverAtSpawn: true));
        var exposed = session.Step(InputFrame.None, 0.375);
        Assert.Equal(0.5d, exposed.DetectionFraction, 5);

        var decaying = session.Step(new InputFrame(Ctrl: true), 0.5);
        Assert.Equal(0.25d, decaying.DetectionFraction, 5);

        var clear = session.Step(new InputFrame(Ctrl: true), 0.5);
        Assert.Equal(0d, clear.DetectionFraction, 5);
    }

    [Fact]
    public void Catch_PreservesFoldersPenalizesTimeAndResetsActors()
    {
        var spawn = TestMaps.P(5, 4);
        var map = TestMaps.Watched(folders: [spawn, TestMaps.P(10, 2), TestMaps.P(10, 6)]);
        var session = new GameSession(map);

        var caught = session.Step(InputFrame.None, GameSession.WalkingDetectionSeconds);

        Assert.Equal(1, caught.CatchCount);
        Assert.True(caught.Folders[0].Collected);
        Assert.Equal(1, caught.CollectedFolderCount);
        Assert.Equal(spawn, caught.PlayerPosition);
        Assert.Equal(map.Guards[0].Spawn, caught.Guards[0].Position);
        Assert.Equal(GameSession.CatchFreezeSeconds, caught.CatchFreezeRemainingSeconds, 5);
        Assert.Equal(GameSession.CatchGraceSeconds, caught.GraceRemainingSeconds, 5);
        Assert.Equal(
            GameSession.RoundDurationSeconds - GameSession.WalkingDetectionSeconds - GameSession.CatchTimePenaltySeconds,
            caught.RemainingSeconds,
            4);
    }

    [Fact]
    public void CatchFreezeAndGrace_BlockInputAndFurtherDetection()
    {
        var session = new GameSession(TestMaps.Watched());
        var caught = session.Step(InputFrame.None, GameSession.WalkingDetectionSeconds);
        var caughtPosition = caught.PlayerPosition;
        var caughtTime = caught.RemainingSeconds;

        var frozen = session.Step(new InputFrame(D: true), GameSession.CatchFreezeSeconds);
        Assert.Equal(caughtPosition, frozen.PlayerPosition);
        Assert.Equal(caughtTime, frozen.RemainingSeconds, 5);
        Assert.Equal(0d, frozen.CatchFreezeRemainingSeconds, 5);

        var protectedSnapshot = session.Step(InputFrame.None, GameSession.CatchGraceSeconds);
        Assert.Equal(0, protectedSnapshot.DetectionFraction, 5);
        Assert.Equal(0d, protectedSnapshot.GraceRemainingSeconds, 5);

        var exposedAgain = session.Step(InputFrame.None, 0.1);
        Assert.True(exposedAgain.DetectionFraction > 0);
    }

    [Fact]
    public void ThirdCatch_LosesRoundWithZeroScore()
    {
        var session = new GameSession(TestMaps.Watched());

        CatchAndWaitForProtection(session);
        CatchAndWaitForProtection(session);
        var lost = session.Step(InputFrame.None, GameSession.WalkingDetectionSeconds);

        Assert.Equal(AppMode.Lost, lost.Mode);
        Assert.Equal(3, lost.CatchCount);
        Assert.Equal(0, lost.Score);
        Assert.Equal(session.Map.Id, lost.ContinuationMapId);
    }

    private static void CatchAndWaitForProtection(GameSession session)
    {
        var caught = session.Step(InputFrame.None, GameSession.WalkingDetectionSeconds);
        Assert.True(caught.CatchCount is 1 or 2);
        session.Step(InputFrame.None, GameSession.CatchFreezeSeconds + GameSession.CatchGraceSeconds);
    }
}
