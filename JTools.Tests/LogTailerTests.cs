using System.Text;
using JTail;

namespace JTools.Tests;

public sealed class LogTailerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"jtools-test-{Guid.NewGuid():N}.log");

    public void Dispose() => File.Delete(_path);

    private void Append(byte[] bytes)
    {
        using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(bytes);
    }

    private void Append(string text) => Append(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Open_reads_the_whole_file_without_a_byte_order_mark()
    {
        Append([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes("one\ntwo\n")]);

        LogTailer.Open(_path, out var initialText);

        Assert.Equal("one\ntwo\n", initialText);
    }

    [Fact]
    public void Reads_only_what_was_appended_since_the_last_read()
    {
        Append("one\n");
        var tailer = LogTailer.Open(_path, out _);

        Assert.Null(tailer.ReadAppendedText());
        Append("two\n");
        Assert.Equal("two\n", tailer.ReadAppendedText());
        Assert.Null(tailer.ReadAppendedText());
    }

    [Fact]
    public void Resumes_from_the_new_end_after_the_file_is_truncated()
    {
        Append("a long first line\n");
        var tailer = LogTailer.Open(_path, out _);

        File.WriteAllText(_path, "new\n");
        Assert.Null(tailer.ReadAppendedText());
        Append("next\n");
        Assert.Equal("next\n", tailer.ReadAppendedText());
    }

    [Fact]
    public void Keeps_a_character_whose_bytes_arrive_in_two_writes()
    {
        Append("caf");
        var tailer = LogTailer.Open(_path, out _);
        var e = Encoding.UTF8.GetBytes("é"); // two bytes

        Append([e[0]]);
        var first = tailer.ReadAppendedText();
        Append([e[1], (byte)'\n']);
        var second = tailer.ReadAppendedText();

        Assert.Equal("é\n", first + second);
    }
}
