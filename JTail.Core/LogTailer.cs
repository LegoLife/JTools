using System.Text;

namespace JTail;

/// <summary>
/// Reads a text file and reports content appended after that point.
/// Opens the file only for the duration of each read so it never holds a lock
/// between polls, letting other processes write to it freely.
/// </summary>
public sealed class LogTailer
{
    private readonly string _path;
    private long _position;

    // Keeps a partial character between reads, so one whose bytes arrive in two writes isn't garbled.
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    private LogTailer(string path, long position)
    {
        _path = path;
        _position = position;
    }

    public string FilePath => _path;

    public long Length => _position;

    /// <summary>
    /// Opens the file, reads its entire contents, and records the end-of-file position so
    /// subsequent calls to <see cref="ReadAppendedText"/> pick up from there.
    /// </summary>
    public static LogTailer Open(string path, out string initialText)
    {
        using var stream = OpenShared(path);
        var length = stream.Length;
        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, buffer.Length);

        // Skip a UTF-8 byte order mark so it doesn't show up as a stray character.
        var preamble = Encoding.UTF8.Preamble;
        var start = buffer.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var tailer = new LogTailer(path, length);
        initialText = tailer.Decode(buffer, start, buffer.Length - start);
        return tailer;
    }

    /// <summary>
    /// Reads any bytes appended to the file since the last read. Returns null if the file
    /// was truncated (shorter than before) or if nothing new was appended, and an empty string
    /// if only part of a character has arrived so far.
    /// </summary>
    public string? ReadAppendedText()
    {
        using var stream = OpenShared(_path);
        var length = stream.Length;

        if (length < _position)
        {
            // File was truncated or replaced; out of scope to recover, just resync to current end.
            _position = length;
            _decoder.Reset();
            return null;
        }

        if (length == _position)
        {
            return null;
        }

        stream.Seek(_position, SeekOrigin.Begin);
        var buffer = new byte[length - _position];
        var read = stream.Read(buffer, 0, buffer.Length);
        _position += read;

        return Decode(buffer, 0, read);
    }

    private string Decode(byte[] bytes, int index, int count)
    {
        var chars = new char[Encoding.UTF8.GetMaxCharCount(count)];
        var decoded = _decoder.GetChars(bytes, index, count, chars, 0, flush: false);
        return new string(chars, 0, decoded);
    }

    private static FileStream OpenShared(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);
}
