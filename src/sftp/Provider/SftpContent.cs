using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;

namespace sftp;
/// <summary>Reads a remote file for Get-Content: line by line, as one string with -Raw, or as bytes with -AsByteStream.</summary>
internal sealed class SftpContentReader : IContentReader {
    private readonly Stream _stream;
    private readonly Encoding _encoding;
    private readonly bool _bytes;
    private readonly bool _raw;
    private StreamReader? _reader;
    private bool _rawDone;

    public SftpContentReader(Stream stream, Encoding encoding, bool bytes, bool raw) {
        _stream = stream;
        _encoding = encoding;
        _bytes = bytes;
        _raw = raw;
    }

    private StreamReader Reader => _reader ??= new StreamReader(_stream, _encoding, detectEncodingFromByteOrderMarks: true);

    public IList Read(long readCount) {
        if (_raw) {
            if (_rawDone) {
                return Array.Empty<object>();
            }

            _rawDone = true;
            if (_bytes) {
                using var buffer = new MemoryStream();
                _stream.CopyTo(buffer);

                return new object[] { buffer.ToArray() };
            }

            return new object[] { Reader.ReadToEnd() };
        }

        var results = new List<object>();
        if (_bytes) {
            int count = readCount <= 0 ? int.MaxValue : (int)Math.Min(readCount, int.MaxValue);
            byte[] chunk = new byte[Math.Min(count, 64 * 1024)];
            while (results.Count < count) {
                int read = _stream.Read(chunk, 0, Math.Min(chunk.Length, count - results.Count));
                if (read == 0) {
                    break;
                }

                for (int i = 0; i < read; i++) {
                    results.Add(chunk[i]);
                }
            }

            return results;
        }

        while (readCount <= 0 || results.Count < readCount) {
            string? line = Reader.ReadLine();
            if (line is null) {
                break;
            }

            results.Add(line);
        }

        return results;
    }

    public void Seek(long offset, SeekOrigin origin) {
        _stream.Seek(offset, origin);
        _reader?.DiscardBufferedData();
    }

    public void Close() => Dispose();

    public void Dispose() {
        if (_reader is not null) {
            _reader.Dispose();
        }
        else {
            _stream.Dispose();
        }
    }
}

/// <summary>Writes a remote file for Set-Content and Add-Content. Lines end with \n, as on Unix servers.</summary>
internal sealed class SftpContentWriter(Stream stream, Encoding encoding, bool bytes, bool noNewline, Action closed) : IContentWriter {
    private bool _started;
    private bool _disposed;

    public IList Write(IList content) {
        if (!_started) {
            _started = true;
            if (!bytes && stream.Position == 0) {
                stream.Write(encoding.GetPreamble());
            }
        }

        foreach (object? value in content) {
            object? item = value is PSObject wrapped ? wrapped.BaseObject : value;
            if (bytes) {
                switch (item) {
                    case byte single:
                        stream.WriteByte(single);
                        break;
                    case byte[] array:
                        stream.Write(array);
                        break;
                    default:
                        throw new PSArgumentException($"-AsByteStream writes bytes, not {item?.GetType().Name ?? "null"}.");
                }

                continue;
            }

            string text = item?.ToString() ?? "";
            stream.Write(encoding.GetBytes(noNewline ? text : text + "\n"));
        }

        return content;
    }

    public void Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public void Close() => Dispose();

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        stream.Dispose();
        closed();
    }
}
