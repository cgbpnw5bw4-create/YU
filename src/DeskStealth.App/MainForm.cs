using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DeskStealth.Core;
using DeskStealth.Rendering;
using DeskStealth.Settings;

namespace DeskStealth;

internal sealed class MainForm : Form
{
    private const double FixedStepSeconds = 1d / 30d;
    private const int MaximumCatchUpSteps = 3;

    private readonly TimeProvider _timeProvider;
    private readonly bool _simulateTrayFailure;
    private readonly SettingsStore _settingsStore = new();
    private readonly GameRenderer _renderer = new();
    private readonly Bitmap _backBuffer = new(
        GameRenderer.CanvasWidth,
        GameRenderer.CanvasHeight,
        PixelFormat.Format32bppPArgb);
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 33 };
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly HashSet<Keys> _pressedKeys = [];

    private AppSettings _settings;
    private GameSession _session;
    private WorldPoint _playerFacing = new(0, 1);
    private long _lastFrameTimestamp;
    private double _accumulatorSeconds;
    private bool _resultStored;
    private bool _closing;
    private bool _disposed;
    private bool _escapePressed;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _trayMenu;
    private ToolStripMenuItem? _topMostMenuItem;
    private ToolStripMenuItem? _scaleMenuItem;

    public MainForm(TimeProvider timeProvider, bool simulateTrayFailure = false)
    {
        _timeProvider = timeProvider;
        _simulateTrayFailure = simulateTrayFailure;
        _backBuffer.SetResolution(96f, 96f);
        _settings = _settingsStore.Load();
        _session = new GameSession(MapCatalog.First, AppMode.Ready);
        _session.EnterClock();

        Text = "DeskStealth";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = _settings.TopMost;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(5, 15, 31);
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _frameTimer.Tick += FrameTimerOnTick;
        _clockTimer.Tick += (_, _) => Invalidate();

        InitializeTray();
        ApplyClientSizeForCurrentDpi();
        _clockTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    public void ShowClockFromSecondInstance()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(ShowClockFromSecondInstance);
            return;
        }

        EnterClock();
        WindowState = FormWindowState.Normal;
        Show();
        ClampToWorkingArea();
        BringToFront();
        Activate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RestoreOrSetDefaultPosition();
        EnterClock();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var snapshot = _session.Snapshot;
        var mapIndex = GetMapIndex(_session.Map);
        var bestScore = _settings.BestScores[Math.Clamp(mapIndex, 0, _settings.BestScores.Length - 1)];
        var animationAlternate = (_stopwatch.ElapsedMilliseconds / 180) % 2 == 1;
        _renderer.Render(
            _backBuffer,
            snapshot,
            _session.Map,
            _playerFacing,
            _timeProvider.GetLocalNow(),
            bestScore,
            animationAlternate);

        e.Graphics.Clear(BackColor);
        e.Graphics.PageUnit = GraphicsUnit.Pixel;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
        e.Graphics.DrawImage(
            _backBuffer,
            ClientRectangle,
            0,
            0,
            GameRenderer.CanvasWidth,
            GameRenderer.CanvasHeight,
            GraphicsUnit.Pixel);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (!_escapePressed)
            {
                _escapePressed = true;
                ToggleClock();
                // ToggleClock clears gameplay input. Keep Escape latched until KeyUp
                // so keyboard auto-repeat cannot immediately toggle back.
                _escapePressed = true;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        var firstPress = _pressedKeys.Add(e.KeyCode);

        if (_session.Snapshot.Mode == AppMode.Clock)
        {
            if (IsMovementKey(e.KeyCode) && IsShiftPressed())
            {
                NudgeWindow(e.KeyCode);
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter && firstPress && _session.Snapshot.Mode is AppMode.Won or AppMode.Lost)
        {
            StartRoundFromResult();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (IsMovementKey(e.KeyCode))
        {
            UpdatePlayerFacing();
        }

        if (IsGameKey(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _escapePressed = false;
        }

        _pressedKeys.Remove(e.KeyCode);
        if (IsMovementKey(e.KeyCode))
        {
            UpdatePlayerFacing();
        }

        if (IsGameKey(e.KeyCode))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyUp(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!_closing)
        {
            EnterClock();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var logicalY = ClientSize.Height <= 0
            ? 0
            : e.Y * GameRenderer.CanvasHeight / ClientSize.Height;
        if (e.Button == MouseButtons.Left &&
            (_session.Snapshot.Mode == AppMode.Clock || logicalY < MapDefinition.HudHeight))
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WmNcButtonDown, NativeMethods.HtCaption, 0);
            ClampToWorkingArea();
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyClientSizeForCurrentDpi();
        BeginInvoke(ClampToWorkingArea);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
        {
            var releasedKey = (Keys)((long)message.WParam & (long)Keys.KeyCode);
            _pressedKeys.Remove(releasedKey);
            if (releasedKey == Keys.Escape)
            {
                _escapePressed = false;
            }
        }

        if (message.Msg == NativeMethods.WmDisplayChange && IsHandleCreated && !_closing)
        {
            BeginInvoke(ClampToWorkingArea);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        SaveSettings();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _frameTimer.Stop();
            _clockTimer.Stop();
            _frameTimer.Dispose();
            _clockTimer.Dispose();
            _notifyIcon?.Dispose();
            _trayMenu?.Dispose();
            _backBuffer.Dispose();
            _renderer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeTray()
    {
        try
        {
            if (_simulateTrayFailure)
            {
                throw new InvalidOperationException("Simulated tray initialization failure.");
            }

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("显示时钟", null, (_, _) => ShowClockAndActivate());
            _trayMenu.Items.Add("恢复游戏（暂停）", null, (_, _) => ShowGameAndActivate());
            _trayMenu.Items.Add(new ToolStripSeparator());

            _topMostMenuItem = new ToolStripMenuItem("窗口置顶")
            {
                Checked = _settings.TopMost,
                CheckOnClick = true
            };
            _topMostMenuItem.CheckedChanged += (_, _) =>
            {
                _settings.TopMost = _topMostMenuItem.Checked;
                TopMost = _settings.TopMost;
                SaveSettings();
            };
            _trayMenu.Items.Add(_topMostMenuItem);

            _scaleMenuItem = new ToolStripMenuItem("窗口缩放");
            AddScaleMenuItem("1×（256×160）", 1f);
            AddScaleMenuItem("1.25×（320×200）", 1.25f);
            AddScaleMenuItem("1.5×（384×240）", 1.5f);
            UpdateScaleChecks();
            _trayMenu.Items.Add(_scaleMenuItem);

            _trayMenu.Items.Add("重置窗口位置", null, (_, _) =>
            {
                PositionAtBottomRight();
                SaveSettings();
            });
            _trayMenu.Items.Add("清除本地数据", null, (_, _) => ClearLocalData());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出", null, (_, _) => Close());

            _notifyIcon = new NotifyIcon
            {
                Text = "DeskStealth - 微型休息游戏",
                Icon = SystemIcons.Information,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _notifyIcon.MouseClick += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    ShowClockAndActivate();
                }
            };
            ShowInTaskbar = false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or SystemException)
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _trayMenu?.Dispose();
            _trayMenu = null;
            ShowInTaskbar = true;
        }
    }

    private void AddScaleMenuItem(string text, float scale)
    {
        if (_scaleMenuItem is null)
        {
            return;
        }

        var item = new ToolStripMenuItem(text) { Tag = scale };
        item.Click += (_, _) => ApplyScale(scale);
        _scaleMenuItem.DropDownItems.Add(item);
    }

    private void UpdateScaleChecks()
    {
        if (_scaleMenuItem is null)
        {
            return;
        }

        foreach (ToolStripItem item in _scaleMenuItem.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.Tag is float scale)
            {
                menuItem.Checked = Math.Abs(scale - _settings.Scale) < 0.001f;
            }
        }
    }

    private void FrameTimerOnTick(object? sender, EventArgs e)
    {
        var currentTimestamp = Stopwatch.GetTimestamp();
        var elapsedSeconds = (currentTimestamp - _lastFrameTimestamp) / (double)Stopwatch.Frequency;
        _lastFrameTimestamp = currentTimestamp;
        _accumulatorSeconds += Math.Min(elapsedSeconds, FixedStepSeconds * MaximumCatchUpSteps);

        var steps = 0;
        while (_accumulatorSeconds >= FixedStepSeconds && steps < MaximumCatchUpSteps)
        {
            _session.Step(CurrentInput(), FixedStepSeconds);
            _accumulatorSeconds -= FixedStepSeconds;
            steps++;
        }

        var snapshot = _session.Snapshot;
        if (snapshot.Mode is AppMode.Won or AppMode.Lost)
        {
            StoreResultIfNeeded(snapshot);
            StopSimulationLoop();
        }

        Invalidate();
    }

    private InputFrame CurrentInput() => new(
        W: IsPressed(Keys.W),
        A: IsPressed(Keys.A),
        S: IsPressed(Keys.S),
        D: IsPressed(Keys.D),
        Shift: IsShiftPressed(),
        Ctrl: IsControlPressed());

    private void ToggleClock()
    {
        if (_session.Snapshot.Mode == AppMode.Clock)
        {
            LeaveClock();
        }
        else
        {
            EnterClock();
        }
    }

    private void EnterClock()
    {
        ClearPressedKeys();
        _session.EnterClock();
        StopSimulationLoop();
        _clockTimer.Start();
        Invalidate();
    }

    private void LeaveClock()
    {
        ClearPressedKeys();
        var snapshot = _session.LeaveClock();
        _clockTimer.Stop();
        if (snapshot.Mode is AppMode.Ready or AppMode.Playing)
        {
            StartSimulationLoop();
        }
        else
        {
            StopSimulationLoop();
        }

        Invalidate();
    }

    private void StartRoundFromResult()
    {
        var result = _session.Snapshot;
        var nextMap = result.Mode == AppMode.Won ? _session.ContinuationMap : _session.Map;
        _session = new GameSession(nextMap, AppMode.Ready);
        _resultStored = false;
        ClearPressedKeys();
        StartSimulationLoop();
        Invalidate();
    }

    private void StartSimulationLoop()
    {
        _lastFrameTimestamp = Stopwatch.GetTimestamp();
        _accumulatorSeconds = 0;
        _frameTimer.Start();
    }

    private void StopSimulationLoop()
    {
        _frameTimer.Stop();
        _accumulatorSeconds = 0;
    }

    private void StoreResultIfNeeded(GameSnapshot snapshot)
    {
        if (_resultStored || snapshot.Mode != AppMode.Won)
        {
            _resultStored = true;
            return;
        }

        var index = GetMapIndex(_session.Map);
        if (index >= 0 && index < _settings.BestScores.Length && snapshot.Score > _settings.BestScores[index])
        {
            _settings.BestScores[index] = snapshot.Score;
            SaveSettings();
        }

        _resultStored = true;
    }

    private void ShowClockAndActivate()
    {
        EnterClock();
        WindowState = FormWindowState.Normal;
        Show();
        ClampToWorkingArea();
        BringToFront();
        Activate();
    }

    private void ShowGameAndActivate()
    {
        if (_session.Snapshot.Mode == AppMode.Clock)
        {
            LeaveClock();
        }

        WindowState = FormWindowState.Normal;
        Show();
        ClampToWorkingArea();
        BringToFront();
        Activate();
    }

    private void ApplyScale(float scale)
    {
        _settings.Scale = scale;
        _settings.Normalize();
        ApplyClientSizeForCurrentDpi();
        ClampToWorkingArea();
        UpdateScaleChecks();
        SaveSettings();
    }

    private void ApplyClientSizeForCurrentDpi()
    {
        ClientSize = WindowMetrics.ClientSizeFor(DeviceDpi, _settings.Scale);
    }

    private void RestoreOrSetDefaultPosition()
    {
        if (_settings.WindowX.HasValue && _settings.WindowY.HasValue)
        {
            Location = new Point(_settings.WindowX.Value, _settings.WindowY.Value);
            ClampToWorkingArea();
        }
        else
        {
            PositionAtBottomRight();
        }
    }

    private void PositionAtBottomRight()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        var margin = (int)Math.Round(12 * DeviceDpi / 96f);
        Location = new Point(area.Right - Width - margin, area.Bottom - Height - margin);
        ClampToWorkingArea();
    }

    private void ClampToWorkingArea()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var area = Screen.FromRectangle(Bounds).WorkingArea;
        Location = WindowMetrics.ClampLocation(area, Size, Location);
    }

    private void NudgeWindow(Keys direction)
    {
        var step = (int)Math.Round(8 * DeviceDpi / 96f);
        var offset = direction switch
        {
            Keys.W => new Point(0, -step),
            Keys.A => new Point(-step, 0),
            Keys.S => new Point(0, step),
            Keys.D => new Point(step, 0),
            _ => Point.Empty
        };
        Location = new Point(Left + offset.X, Top + offset.Y);
        ClampToWorkingArea();
    }

    private void ClearLocalData()
    {
        _settingsStore.Clear();
        _settings = new AppSettings();
        TopMost = _settings.TopMost;
        if (_topMostMenuItem is not null)
        {
            _topMostMenuItem.Checked = _settings.TopMost;
        }

        ApplyScale(_settings.Scale);
        PositionAtBottomRight();
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (!IsDisposed && WindowState == FormWindowState.Normal)
        {
            _settings.WindowX = Left;
            _settings.WindowY = Top;
        }

        _settingsStore.Save(_settings);
    }

    private void ClearPressedKeys()
    {
        _pressedKeys.Clear();
        _escapePressed = false;
    }

    private bool IsPressed(Keys key) => _pressedKeys.Contains(key);

    private bool IsShiftPressed() =>
        IsPressed(Keys.ShiftKey) || IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey) ||
        (ModifierKeys & Keys.Shift) == Keys.Shift;

    private bool IsControlPressed() =>
        IsPressed(Keys.ControlKey) || IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey) ||
        (ModifierKeys & Keys.Control) == Keys.Control;

    private void UpdatePlayerFacing()
    {
        var direction = CurrentInput().Direction;
        if (direction.Length > 0.001d)
        {
            _playerFacing = direction;
        }
    }

    private static bool IsMovementKey(Keys key) => key is Keys.W or Keys.A or Keys.S or Keys.D;

    private static bool IsGameKey(Keys key) =>
        IsMovementKey(key) ||
        key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Enter or Keys.Escape;

    private static int GetMapIndex(MapDefinition map)
    {
        for (var index = 0; index < MapCatalog.All.Length; index++)
        {
            if (string.Equals(MapCatalog.All[index].Id, map.Id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }
}
