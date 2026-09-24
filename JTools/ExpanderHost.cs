using Avalonia.Threading;
using TextExpander;

namespace JTools;

/// <summary>
/// Owns the text-expansion engine for the app: the abbreviation list, settings, and keyboard hook.
/// The hook only runs if no other process (e.g. the standalone Text Expander) is already expanding.
/// </summary>
public sealed class ExpanderHost : IDisposable
{
    private readonly Mutex? _lock;
    private readonly KeyboardHook? _hook;

    public SnippetStore Store { get; } = new();

    public AppSettings Settings { get; } = AppSettings.Load();

    /// <summary>False when another process holds the expander, so expansion can't run here.</summary>
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

    public event Action? EnabledChanged;

    public ExpanderHost()
    {
        Store.Load();

        _lock = ExpanderLock.TryAcquire();
        if (_lock is null)
        {
            return;
        }

        // The hook posts expansions back to the UI thread through the current synchronization context.
        if (SynchronizationContext.Current is null)
        {
            SynchronizationContext.SetSynchronizationContext(new AvaloniaSynchronizationContext());
        }
        _hook = new KeyboardHook(Store, Settings);
    }

    public void Dispose()
    {
        _hook?.Dispose();
        if (_lock is not null)
        {
            _lock.ReleaseMutex();
            _lock.Dispose();
        }
    }
}
