using System.Collections.Immutable;

namespace DeskStealth.Core;

public enum AppMode
{
    Clock,
    Ready,
    Playing,
    Won,
    Lost,
}

public enum MovementMode
{
    Idle,
    Walking,
    Sprinting,
    Crouching,
}

/// <summary>One fixed-step input sample. No global keyboard state is read by the core.</summary>
public readonly record struct InputFrame(
    bool W = false,
    bool A = false,
    bool S = false,
    bool D = false,
    bool Shift = false,
    bool Ctrl = false)
{
    public static InputFrame None => default;

    public bool HasMovement => W || A || S || D;

    public WorldPoint Direction
    {
        get
        {
            var horizontal = (D ? 1d : 0d) - (A ? 1d : 0d);
            var vertical = (S ? 1d : 0d) - (W ? 1d : 0d);
            return new WorldPoint(horizontal, vertical).Normalized();
        }
    }
}

public sealed record GuardSnapshot(
    WorldPoint Position,
    double FacingDegrees,
    int TargetWaypointIndex,
    double PauseRemainingSeconds,
    bool SeesPlayer);

public sealed record FolderSnapshot(int Index, WorldPoint Position, bool Collected);

/// <summary>An immutable rendering/read-model returned after every simulation step.</summary>
public sealed record GameSnapshot(
    AppMode Mode,
    string MapId,
    string MapName,
    string ContinuationMapId,
    WorldPoint PlayerPosition,
    MovementMode MovementMode,
    bool IsHidden,
    ImmutableArray<GuardSnapshot> Guards,
    ImmutableArray<FolderSnapshot> Folders,
    WorldRect ExitZone,
    bool ExitUnlocked,
    double RemainingSeconds,
    double StaminaFraction,
    double SprintRechargeDelayRemainingSeconds,
    double DetectionFraction,
    int CatchCount,
    double CatchFreezeRemainingSeconds,
    double GraceRemainingSeconds,
    double ReadyRemainingSeconds,
    int Score)
{
    public int CollectedFolderCount => Folders.Count(folder => folder.Collected);
}
