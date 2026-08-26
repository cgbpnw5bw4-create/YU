namespace DeskStealth;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly ManualResetEvent _shutdown = new(false);
    private readonly string _activationEventName;
    private Thread? _listener;

    public SingleInstanceCoordinator(string instanceId = "v1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var mutexName = $@"Local\DeskStealth.Singleton.{instanceId}";
        _activationEventName = $@"Local\DeskStealth.Activate.{instanceId}";
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        IsPrimary = createdNew;

        if (createdNew)
        {
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _activationEventName);
        }
    }

    public bool IsPrimary { get; }

    public void StartListening(Action activationAction)
    {
        if (!IsPrimary || _activationEvent is null || _listener is not null)
        {
            return;
        }

        _listener = new Thread(() =>
        {
            var handles = new WaitHandle[] { _activationEvent, _shutdown };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                activationAction();
            }
        })
        {
            IsBackground = true,
            Name = "DeskStealth activation listener"
        };
        _listener.Start();
    }

    public void SignalPrimary()
    {
        if (IsPrimary)
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var activation = EventWaitHandle.OpenExisting(_activationEventName);
                activation.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 4)
            {
                Thread.Sleep(80);
            }
        }
    }

    public void Dispose()
    {
        _shutdown.Set();
        _listener?.Join(500);
        _activationEvent?.Dispose();
        _shutdown.Dispose();

        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already shutting down; the OS will release the handle.
            }
        }

        _mutex.Dispose();
    }
}
