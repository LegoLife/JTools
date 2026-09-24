using System.Text;

namespace JTail;

/// <summary>
/// Reads the tail of a text file and reports content appended after that point.
/// Opens the file only for the duration of each read so it never holds a lock
/// between polls, letting other processes write to it freely.
/// </summary>
public sealed class LogTailer
{
    private const int InitialLineCount = 100;
    private const int ReadChunkSize = 64 * 1024;

    private readonly string _path;
    private long _position;

    private LogTailer(string path, long position)
    {
        _path = path;
        _position = position;
    }

    public string FilePath => _path;

    public long Length => _position;

    /// <summary>
    /// Opens the file, reads its last <see cref="InitialLineCount"/> lines, and records the
    /// end-of-file position so subsequent calls to <see cref="ReadAppendedText"/> pick up from there.
    /// </summary>
    public static LogTailer OpenAtTail(string path, out string initialText)
    {
        using var stream = OpenShared(path);
        initialText = ReadLastLines(stream, InitialLineCount);
        return new LogTailer(path, stream.Length);
    }

    /// <summary>
    /// Reads any bytes appended to the file since the last read. Returns null if the file
    /// was truncated (shorter than before) or if nothing new was appended.
    /// </summary>
    public string? ReadAppendedText()
    {
        using var stream = OpenShared(_path);
        var length = stream.Length;

        if (length < _position)
        {
            // File was truncated or replaced; out of scope to recover, just resync to current end.
            _position = length;
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

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static FileStream OpenShared(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);

    private static string ReadLastLines(FileStream stream, int lineCount)
    {
        var length = stream.Length;
        if (length == 0)
        {
            return string.Empty;
        }

        var newlinesFound = 0;
        var position = length;
        var chunks = new List<byte[]>();

        while (position > 0 && newlinesFound <= lineCount)
        {
            var chunkSize = (int)Math.Min(ReadChunkSize, position);
            position -= chunkSize;

            var buffer = new byte[chunkSize];
            stream.Seek(position, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, chunkSize);

            for (var i = chunkSize - 1; i >= 0; i--)
            {
                if (buffer[i] == (byte)'\n')
                {
                    newlinesFound++;
                    if (newlinesFound > lineCount)
                    {
                        position += i + 1;
                        break;
                    }
                }
            }

            chunks.Add(buffer);
        }

        chunks.Reverse();
        var combined = new byte[chunks.Sum(c => c.Length)];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }

        var text = Encoding.UTF8.GetString(combined);
        var lines = text.Split('\n');
        var lastLines = lines.Skip(Math.Max(0, lines.Length - lineCount));
        return string.Join('\n', lastLines);
    }
}
