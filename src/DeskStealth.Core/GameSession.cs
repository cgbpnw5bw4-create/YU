using System.Collections.Immutable;

namespace DeskStealth.Core;

/// <summary>
/// Deterministic game simulation. The caller supplies both input and elapsed time;
/// this type never reads WinForms state, the system clock, or global randomness.
/// </summary>
public sealed class GameSession
{
    public const double RoundDurationSeconds = 60d;
    public const double WalkingSpeed = 48d;
    public const double SprintingSpeed = 76d;
    public const double CrouchingSpeed = 28d;
    public const double SprintCapacitySeconds = 2d;
    public const double SprintRechargeDelaySeconds = 0.75d;
    public const double SprintFullRechargeSeconds = 2.5d;
    public const double GuardSpeed = 24d;
    public const double GuardWaypointPauseSeconds = 0.4d;
    public const double GuardMaximumTurnDegreesPerSecond = 180d;
    public const double GuardViewAngleDegrees = 70d;
    public const double GuardViewRange = 64d;
    public const double WalkingDetectionSeconds = 0.75d;
    public const double SprintingDetectionSeconds = 0.5d;
    public const double CrouchingDetectionSeconds = 1d;
    public const double DetectionDecaySeconds = 1d;
    public const double CatchTimePenaltySeconds = 8d;
    public const double CatchFreezeSeconds = 0.6d;
    public const double CatchGraceSeconds = 0.8d;
    public const double ReadyGateSeconds = 1d;
    public const double PlayerRadius = 4d;
    public const double FolderPickupRadius = 8d;

    private const double MaximumSimulationSliceSeconds = 1d / 60d;

    private readonly bool[] _collectedFolders;
    private readonly GuardRuntime[] _guards;
    private readonly bool[] _guardSeesPlayer;
    private AppMode _mode;
    private AppMode _modeBeforeClock;
    private WorldPoint _playerPosition;
    private MovementMode _movementMode;
    private double _remainingSeconds;
    private double _staminaSeconds;
    private double _sprintRechargeDelay;
    private double _detectionFraction;
    private double _detectionDecayStartFraction;
    private double _detectionDecayElapsed;
    private bool _isDetectionDecaying;
    private int _catchCount;
    private double _catchFreezeRemaining;
    private double _graceRemaining;
    private double _readyRemaining;
    private int _score;
    private bool _isHidden;

    public GameSession(MapDefinition map, AppMode initialMode = AppMode.Playing)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (initialMode is AppMode.Won or AppMode.Lost)
        {
            throw new ArgumentOutOfRangeException(nameof(initialMode), "A new game cannot start already resolved.");
        }

        Map = map;
        _collectedFolders = new bool[map.FolderPositions.Length];
        _guards = map.Guards.Select(definition => new GuardRuntime(definition)).ToArray();
        _guardSeesPlayer = new bool[_guards.Length];
        ResetState(initialMode);
    }

    public MapDefinition Map { get; }

    /// <summary>A read model of current state without advancing the simulation.</summary>
    public GameSnapshot Snapshot => CreateSnapshot();

    /// <summary>
    /// The UI-owned campaign continuation: the next catalog map after a win,
    /// otherwise this same map for retry.
    /// </summary>
    public MapDefinition ContinuationMap => _mode == AppMode.Won ? MapCatalog.Next(Map) : Map;

    public GameSnapshot Step(InputFrame input, double fixedDeltaSeconds)
    {
        if (!double.IsFinite(fixedDeltaSeconds) || fixedDeltaSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Delta time must be finite and non-negative.");
        }

        if (fixedDeltaSeconds <= Geometry.Epsilon || _mode is AppMode.Clock or AppMode.Won or AppMode.Lost)
        {
            return CreateSnapshot();
        }

        var playableDelta = ConsumeReadyGate(input, fixedDeltaSeconds);
        if (_mode != AppMode.Playing || playableDelta <= Geometry.Epsilon)
        {
            return CreateSnapshot();
        }

        var slices = Math.Max(1, (int)Math.Ceiling(playableDelta / MaximumSimulationSliceSeconds));
        var sliceSeconds = playableDelta / slices;
        for (var index = 0; index < slices && _mode == AppMode.Playing; index++)
        {
            SimulateSlice(input, sliceSeconds);
        }

        return CreateSnapshot();
    }

    /// <summary>Pauses immediately in a neutral clock state. No simulation runs in this mode.</summary>
    public GameSnapshot EnterClock()
    {
        if (_mode != AppMode.Clock)
        {
            _modeBeforeClock = _mode;
            _mode = AppMode.Clock;
            Array.Fill(_guardSeesPlayer, false);
        }

        return CreateSnapshot();
    }

    /// <summary>
    /// Leaves the clock. An active round enters a one-second Ready gate and then
    /// waits for a movement frame; resolved rounds return to their result screen.
    /// </summary>
    public GameSnapshot LeaveClock()
    {
        if (_mode != AppMode.Clock)
        {
            return CreateSnapshot();
        }

        if (_modeBeforeClock is AppMode.Playing or AppMode.Ready)
        {
            _mode = AppMode.Ready;
            _readyRemaining = ReadyGateSeconds;
        }
        else
        {
            _mode = _modeBeforeClock;
        }

        return CreateSnapshot();
    }

    /// <summary>Resets this map for retry and applies the standard one-second Ready gate.</summary>
    public GameSnapshot Restart()
    {
        ResetState(AppMode.Ready);
        return CreateSnapshot();
    }

    private void ResetState(AppMode initialMode)
    {
        Array.Fill(_collectedFolders, false);
        _playerPosition = Map.PlayerSpawn;
        _movementMode = MovementMode.Idle;
        _remainingSeconds = RoundDurationSeconds;
        _staminaSeconds = SprintCapacitySeconds;
        _sprintRechargeDelay = 0;
        _detectionFraction = 0;
        _detectionDecayStartFraction = 0;
        _detectionDecayElapsed = 0;
        _isDetectionDecaying = false;
        _catchCount = 0;
        _catchFreezeRemaining = 0;
        _graceRemaining = 0;
        _score = 0;
        _isHidden = false;
        _mode = initialMode;
        // Starting directly in Clock is supported for the desktop app's startup state;
        // leaving that clock begins a normal gated round.
        _modeBeforeClock = initialMode == AppMode.Clock ? AppMode.Playing : initialMode;
        _readyRemaining = initialMode == AppMode.Ready ? ReadyGateSeconds : 0;
        foreach (var guard in _guards)
        {
            guard.Reset();
        }

        Array.Fill(_guardSeesPlayer, false);
    }

    private double ConsumeReadyGate(InputFrame input, double deltaSeconds)
    {
        if (_mode != AppMode.Ready)
        {
            return deltaSeconds;
        }

        var gateTime = Math.Min(deltaSeconds, _readyRemaining);
        _readyRemaining = Math.Max(0, _readyRemaining - gateTime);
        var remainingDelta = deltaSeconds - gateTime;

        if (_readyRemaining <= Geometry.Epsilon && input.HasMovement)
        {
            _readyRemaining = 0;
            _mode = AppMode.Playing;
        }

        return remainingDelta;
    }

    private void SimulateSlice(InputFrame input, double deltaSeconds)
    {
        if (_catchFreezeRemaining > Geometry.Epsilon)
        {
            var frozenTime = Math.Min(deltaSeconds, _catchFreezeRemaining);
            _catchFreezeRemaining = Math.Max(0, _catchFreezeRemaining - frozenTime);
            deltaSeconds -= frozenTime;
            if (deltaSeconds <= Geometry.Epsilon)
            {
                _movementMode = MovementMode.Idle;
                _isHidden = false;
                return;
            }
        }

        var protectedByGrace = _graceRemaining > Geometry.Epsilon;

        var direction = input.Direction;
        var isMoving = direction.Length > Geometry.Epsilon;
        var sprintRequested = isMoving && input.Shift && !input.Ctrl;
        var canSprint = sprintRequested && _staminaSeconds > Geometry.Epsilon;

        _movementMode = input.Ctrl
            ? MovementMode.Crouching
            : canSprint
                ? MovementMode.Sprinting
                : isMoving
                    ? MovementMode.Walking
                    : MovementMode.Idle;

        UpdateStamina(sprintRequested, canSprint, deltaSeconds);

        var speed = _movementMode switch
        {
            MovementMode.Crouching => CrouchingSpeed,
            MovementMode.Sprinting => SprintingSpeed,
            _ => WalkingSpeed,
        };

        if (isMoving)
        {
            MovePlayer(direction * (speed * deltaSeconds));
        }

        _isHidden = input.Ctrl && Map.IsInCover(_playerPosition);
        CollectFolders();
        UpdateGuards(deltaSeconds, protectedByGrace);
        UpdateDetection(deltaSeconds);

        if (protectedByGrace)
        {
            _graceRemaining = Math.Max(0, _graceRemaining - deltaSeconds);
        }

        if (_mode != AppMode.Playing)
        {
            return;
        }

        if (_collectedFolders.All(collected => collected) && Map.ExitZone.Contains(_playerPosition))
        {
            WinRound();
            return;
        }

        _remainingSeconds = Math.Max(0, _remainingSeconds - deltaSeconds);
        if (_remainingSeconds <= Geometry.Epsilon)
        {
            LoseRound();
        }
    }

    private void UpdateStamina(bool sprintRequested, bool sprinting, double deltaSeconds)
    {
        if (sprinting)
        {
            _staminaSeconds = Math.Max(0, _staminaSeconds - deltaSeconds);
            _sprintRechargeDelay = SprintRechargeDelaySeconds;
            return;
        }

        // Holding an exhausted sprint key is not a rest; release Shift, stop, or crouch to recharge.
        if (sprintRequested)
        {
            return;
        }

        var rechargeTime = deltaSeconds;
        if (_sprintRechargeDelay > 0)
        {
            var delayTime = Math.Min(rechargeTime, _sprintRechargeDelay);
            _sprintRechargeDelay = Math.Max(0, _sprintRechargeDelay - delayTime);
            rechargeTime -= delayTime;
        }

        if (rechargeTime > 0)
        {
            var rechargePerSecond = SprintCapacitySeconds / SprintFullRechargeSeconds;
            _staminaSeconds = Math.Min(SprintCapacitySeconds, _staminaSeconds + (rechargeTime * rechargePerSecond));
        }
    }

    private void MovePlayer(WorldPoint displacement)
    {
        var horizontal = new WorldPoint(_playerPosition.X + displacement.X, _playerPosition.Y);
        if (!Map.IsSolid(horizontal, PlayerRadius))
        {
            _playerPosition = horizontal;
        }

        var vertical = new WorldPoint(_playerPosition.X, _playerPosition.Y + displacement.Y);
        if (!Map.IsSolid(vertical, PlayerRadius))
        {
            _playerPosition = vertical;
        }
    }

    private void CollectFolders()
    {
        for (var index = 0; index < Map.FolderPositions.Length; index++)
        {
            if (!_collectedFolders[index] &&
                WorldPoint.Distance(_playerPosition, Map.FolderPositions[index]) <= FolderPickupRadius)
            {
                _collectedFolders[index] = true;
            }
        }
    }

    private void UpdateGuards(double deltaSeconds, bool protectedByGrace)
    {
        for (var index = 0; index < _guards.Length; index++)
        {
            _guards[index].Step(deltaSeconds);
            _guardSeesPlayer[index] = !protectedByGrace &&
                                      !_isHidden &&
                                      CanSeePlayer(_guards[index]);
        }
    }

    private bool CanSeePlayer(GuardRuntime guard)
    {
        var toPlayer = _playerPosition - guard.Position;
        var distance = toPlayer.Length;
        if (distance > GuardViewRange || distance <= Geometry.Epsilon)
        {
            return distance <= Geometry.Epsilon;
        }

        var facing = WorldPoint.FromAngleDegrees(guard.FacingDegrees);
        var cosine = WorldPoint.Dot(facing, toPlayer / distance);
        var minimumCosine = Math.Cos((GuardViewAngleDegrees / 2d) * Math.PI / 180d);
        return cosine + Geometry.Epsilon >= minimumCosine && Map.HasClearLine(guard.Position, _playerPosition);
    }

    private void UpdateDetection(double deltaSeconds)
    {
        if (_guardSeesPlayer.Any(sees => sees))
        {
            _isDetectionDecaying = false;
            _detectionDecayElapsed = 0;
            var threshold = _movementMode switch
            {
                MovementMode.Sprinting => SprintingDetectionSeconds,
                MovementMode.Crouching => CrouchingDetectionSeconds,
                _ => WalkingDetectionSeconds,
            };
            _detectionFraction = Math.Min(1d, _detectionFraction + (deltaSeconds / threshold));
        }
        else
        {
            if (_detectionFraction > Geometry.Epsilon && !_isDetectionDecaying)
            {
                _isDetectionDecaying = true;
                _detectionDecayStartFraction = _detectionFraction;
                _detectionDecayElapsed = 0;
            }

            if (_isDetectionDecaying)
            {
                _detectionDecayElapsed = Math.Min(
                    DetectionDecaySeconds,
                    _detectionDecayElapsed + deltaSeconds);
                var decayProgress = _detectionDecayElapsed / DetectionDecaySeconds;
                _detectionFraction = _detectionDecayStartFraction * (1d - decayProgress);
                if (_detectionDecayElapsed >= DetectionDecaySeconds - Geometry.Epsilon)
                {
                    _detectionFraction = 0;
                    _isDetectionDecaying = false;
                }
            }
        }

        if (_detectionFraction >= 1d - Geometry.Epsilon)
        {
            CatchPlayer();
        }
    }

    private void CatchPlayer()
    {
        _catchCount++;
        _remainingSeconds = Math.Max(0, _remainingSeconds - CatchTimePenaltySeconds);
        _detectionFraction = 0;
        _detectionDecayStartFraction = 0;
        _detectionDecayElapsed = 0;
        _isDetectionDecaying = false;
        _playerPosition = Map.PlayerSpawn;
        _movementMode = MovementMode.Idle;
        _isHidden = false;
        _catchFreezeRemaining = CatchFreezeSeconds;
        _graceRemaining = CatchGraceSeconds;
        foreach (var guard in _guards)
        {
            guard.Reset();
        }

        Array.Fill(_guardSeesPlayer, false);

        if (_catchCount >= 3 || _remainingSeconds <= Geometry.Epsilon)
        {
            LoseRound();
        }
    }

    private void WinRound()
    {
        _mode = AppMode.Won;
        var wholeSeconds = (int)Math.Floor(_remainingSeconds + Geometry.Epsilon);
        _score = (wholeSeconds * 100) + (_catchCount == 0 ? 500 : 0);
        Array.Fill(_guardSeesPlayer, false);
    }

    private void LoseRound()
    {
        _mode = AppMode.Lost;
        _score = 0;
        Array.Fill(_guardSeesPlayer, false);
    }

    private GameSnapshot CreateSnapshot()
    {
        var guards = ImmutableArray.CreateBuilder<GuardSnapshot>(_guards.Length);
        for (var index = 0; index < _guards.Length; index++)
        {
            var guard = _guards[index];
            guards.Add(new GuardSnapshot(
                guard.Position,
                guard.FacingDegrees,
                guard.TargetWaypointIndex,
                guard.PauseRemainingSeconds,
                _guardSeesPlayer[index]));
        }

        var folders = ImmutableArray.CreateBuilder<FolderSnapshot>(Map.FolderPositions.Length);
        for (var index = 0; index < Map.FolderPositions.Length; index++)
        {
            folders.Add(new FolderSnapshot(index, Map.FolderPositions[index], _collectedFolders[index]));
        }

        return new GameSnapshot(
            _mode,
            Map.Id,
            Map.Name,
            ContinuationMap.Id,
            _playerPosition,
            _movementMode,
            _isHidden,
            guards.MoveToImmutable(),
            folders.MoveToImmutable(),
            Map.ExitZone,
            _collectedFolders.All(collected => collected),
            _remainingSeconds,
            _staminaSeconds / SprintCapacitySeconds,
            _sprintRechargeDelay,
            _detectionFraction,
            _catchCount,
            _catchFreezeRemaining,
            _graceRemaining,
            _readyRemaining,
            _score);
    }

    private sealed class GuardRuntime
    {
        private readonly GuardDefinition _definition;

        public GuardRuntime(GuardDefinition definition)
        {
            _definition = definition;
            Reset();
        }

        public WorldPoint Position { get; private set; }

        public double FacingDegrees { get; private set; }

        public int TargetWaypointIndex { get; private set; }

        public double PauseRemainingSeconds { get; private set; }

        public void Reset()
        {
            Position = _definition.Spawn;
            FacingDegrees = _definition.InitialFacingDegrees;
            TargetWaypointIndex = 1;
            PauseRemainingSeconds = 0;
        }

        public void Step(double deltaSeconds)
        {
            if (PauseRemainingSeconds > 0)
            {
                PauseRemainingSeconds = Math.Max(0, PauseRemainingSeconds - deltaSeconds);
                return;
            }

            var target = _definition.Waypoints[TargetWaypointIndex];
            var toTarget = target - Position;
            var distance = toTarget.Length;
            if (distance <= Geometry.Epsilon)
            {
                ArriveAtWaypoint();
                return;
            }

            var desiredFacing = Geometry.AngleDegrees(toTarget);
            var turn = Geometry.SignedAngleDifference(FacingDegrees, desiredFacing);
            var maximumTurn = GuardMaximumTurnDegreesPerSecond * deltaSeconds;
            FacingDegrees = Geometry.NormalizeDegrees(FacingDegrees + Math.Clamp(turn, -maximumTurn, maximumTurn));

            var travel = GuardSpeed * deltaSeconds;
            if (travel + Geometry.Epsilon >= distance)
            {
                Position = target;
                ArriveAtWaypoint();
            }
            else
            {
                Position += (toTarget / distance) * travel;
            }
        }

        private void ArriveAtWaypoint()
        {
            PauseRemainingSeconds = GuardWaypointPauseSeconds;
            TargetWaypointIndex = (TargetWaypointIndex + 1) % _definition.Waypoints.Length;
        }
    }
}
