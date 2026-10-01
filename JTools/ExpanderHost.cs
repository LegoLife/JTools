using Avalonia.Threading;
using TextExpander;

namespace JTools;

/// <summary>
/// Owns the text-expansion engine for the app: the abbreviation list, settings, and keyboard hook.
/// Expansion runs between <see cref="Start"/> and <see cref="Stop"/>, and only if no other process
/// (e.g. the standalone Text Expander) is already expanding.
/// </summary>
public sealed class ExpanderHost : IDisposable
{
    private Mutex? _lock;
    private KeyboardHook? _hook;

    public SnippetStore Store { get; } = new();

    public AppSettings Settings { get; } = AppSettings.Load();

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>, i.e. while the Expand tool is turned on.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>False when expansion is stopped or another process holds the expander, so expansion can't run here.</summary>
    public bool IsAvailable => _hook is not null;

    public bool Enabled
    {
        get => _hook?.Enabled ?? false;
        set
        {
            if (_hook is null || _hook.Enabled == value)
            {
                return;
            }

            _hook.Enabled = value;
            EnabledChanged?.Invoke();
        }
    }

    /// <summary>Lets abbreviations expand in JTools' own window, e.g. while the Expand tool's Try it box has focus.</summary>
    public bool ExpandInOwnWindow
    {
        get => _hook?.ExpandInOwnWindow ?? false;
        set
        {
            if (_hook is not null)
            {
                _hook.ExpandInOwnWindow = value;
            }
        }
    }

    public event Action? EnabledChanged;

    /// <summary>Raised when <see cref="IsStarted"/> or <see cref="IsAvailable"/> changes.</summary>
    public event Action? StateChanged;

    public ExpanderHost()
    {
        Store.Load();
    }

    /// <summary>
    /// Takes the expander lock and installs the keyboard hook. If another process holds the lock,
    /// expansion stays unavailable until the next start.
    /// </summary>
    public void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;
        _lock = ExpanderLock.TryAcquire();
        if (_lock is not null)
        {
            // Another process may have changed the abbreviations while it held the lock.
            Store.Load();

            // The hook posts expansions back to the UI thread through the current synchronization context.
            if (SynchronizationContext.Current is null)
            {
                SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext());
            }
            _hook = new KeyboardHook(Store, Settings);
        }
        StateChanged?.Invoke();
    }

    /// <summary>Removes the keyboard hook and releases the lock, so another process can expand.</summary>
    public void Stop()
    {
        if (!IsStarted)
        {
            return;
        }

        IsStarted = false;
        _hook?.Dispose();
        _hook = null;
        if (_lock is not null)
        {
            _lock.ReleaseMutex();
            _lock.Dispose();
            _lock = null;
        }
        StateChanged?.Invoke();
    }

    public void Dispose() => Stop();
}
