using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Roamly.Api.Features.Journal.UploadJournalAttachment;

public static class JournalImageMultipartReader
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    private const int SignatureProbeLength = 12;

    public static async Task<JournalImageUploadParseResult> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetBoundary(context.Request.ContentType, out var boundary))
        {
            return JournalImageUploadParseResult.Invalid();
        }

        var reader = new MultipartReader(boundary, context.Request.Body)
        {
            BodyLengthLimit = MaximumBytes + 1,
        };
        var section = await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false);
        if (section is null || !IsFileSection(section))
        {
            return JournalImageUploadParseResult.Invalid();
        }

        var signature = new byte[SignatureProbeLength];
        var length = await ReadProbeAsync(section.Body, signature, cancellationToken).ConfigureAwait(false);
        var contentType = JournalImageContentType.FromSignature(signature.AsSpan(0, length));
        return contentType is null
            ? JournalImageUploadParseResult.Unsupported()
            : JournalImageUploadParseResult.Ready(
                new JournalImageUploadStream(
                    signature[..length],
                    section.Body,
                    MaximumBytes,
                    async token =>
                    {
                        if (await reader.ReadNextSectionAsync(token).ConfigureAwait(false) is not null)
                        {
                            throw new InvalidDataException("Invia una sola sezione multipart.");
                        }
                    }),
                contentType);
    }

    private static bool TryGetBoundary(string? contentType, out string boundary)
    {
        boundary = string.Empty;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            || !string.Equals(parsed.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || parsed.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name.Value, "boundary", StringComparison.OrdinalIgnoreCase))?.Value is not { } value)
        {
            return false;
        }

        boundary = HeaderUtilities.RemoveQuotes(value).Value ?? string.Empty;
        return boundary.Length is > 0 and <= 128;
    }

    private static bool IsFileSection(MultipartSection section)
    {
        return ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
            && string.Equals(disposition.DispositionType.Value, "form-data", StringComparison.OrdinalIgnoreCase)
            && string.Equals(HeaderUtilities.RemoveQuotes(disposition.Name).Value, "file", StringComparison.Ordinal)
            && (disposition.FileName.HasValue || disposition.FileNameStar.HasValue);
    }

    private static async Task<int> ReadProbeAsync(
        Stream source,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var length = 0;
        while (length < destination.Length)
        {
            var read = await source.ReadAsync(destination[length..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        return length;
    }
}

public sealed record JournalImageUploadParseResult(
    JournalImageUploadStream? Stream,
    string? ContentType,
    bool IsUnsupported)
{
    public bool IsValid => Stream is not null && ContentType is not null;
    public static JournalImageUploadParseResult Invalid() => new(null, null, false);
    public static JournalImageUploadParseResult Unsupported() => new(null, null, true);
    public static JournalImageUploadParseResult Ready(JournalImageUploadStream stream, string contentType) =>
        new(stream, contentType, false);
}

public sealed class JournalImageTooLargeException : IOException;

/// <summary>Prefissa la firma letta e limita in streaming la lettura a 10 MiB.</summary>
public sealed class JournalImageUploadStream(
    byte[] prefix,
    Stream source,
    long maximumBytes,
    Func<CancellationToken, Task>? validateEnd = null) : Stream
{
    private int _prefixOffset;
    private long _bytesRead;
    private bool _sourceCompleted;

    public long BytesRead => _bytesRead;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _bytesRead; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var capacity = GetReadCapacity(buffer.Length);
        var prefixCount = CopyPrefix(buffer[..capacity]);
        var read = prefixCount == capacity ? prefixCount : prefixCount + source.Read(buffer[(prefixCount)..capacity]);
        if (read == 0)
        {
            ValidateEndSynchronously();
        }

        _bytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var capacity = await GetReadCapacityAsync(buffer.Length, cancellationToken).ConfigureAwait(false);
        var prefixCount = CopyPrefix(buffer[..capacity].Span);
        var read = prefixCount == capacity
            ? prefixCount
            : prefixCount + await source.ReadAsync(
                buffer[prefixCount..capacity],
                cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            await ValidateEndAsync(cancellationToken).ConfigureAwait(false);
        }

        _bytesRead += read;
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int GetReadCapacity(int requested)
    {
        var remaining = maximumBytes - _bytesRead;
        if (remaining == 0)
        {
            Span<byte> overflow = stackalloc byte[1];
            if (source.Read(overflow) > 0)
            {
                throw new JournalImageTooLargeException();
            }

            ValidateEndSynchronously();
            return 0;
        }

        return (int)Math.Min(requested, remaining);
    }

    private async ValueTask<int> GetReadCapacityAsync(int requested, CancellationToken cancellationToken)
    {
        var remaining = maximumBytes - _bytesRead;
        if (remaining == 0)
        {
            var overflow = new byte[1];
            if (await source.ReadAsync(overflow, cancellationToken).ConfigureAwait(false) > 0)
            {
                throw new JournalImageTooLargeException();
            }

            await ValidateEndAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        return (int)Math.Min(requested, remaining);
    }

    private int CopyPrefix(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, prefix.Length - _prefixOffset);
        prefix.AsSpan(_prefixOffset, count).CopyTo(destination);
        _prefixOffset += count;
        return count;
    }

    private void ValidateEndSynchronously()
    {
        if (_sourceCompleted)
        {
            return;
        }

        _sourceCompleted = true;
        validateEnd?.Invoke(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task ValidateEndAsync(CancellationToken cancellationToken)
    {
        if (_sourceCompleted)
        {
            return;
        }

        _sourceCompleted = true;
        if (validateEnd is not null)
        {
            await validateEnd(cancellationToken).ConfigureAwait(false);
        }
    }
}
