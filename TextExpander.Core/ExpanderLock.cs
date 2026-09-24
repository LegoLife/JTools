namespace TextExpander;

/// <summary>
/// Ensures only one process on the desktop expands text at a time (e.g. the standalone Text Expander
/// and an app embedding it), since two keyboard hooks would both expand every abbreviation.
/// </summary>
public static class ExpanderLock
{
    private const string MutexName = @"Local\TextExpander.SingleInstance";

    /// <returns>The held lock (keep it alive for the life of the process), or null if another process has it.</returns>
    public static Mutex? TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out bool acquired);
        if (acquired)
            return mutex;

        mutex.Dispose();
        return null;
    }
}
