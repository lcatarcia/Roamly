using System.Text;
using Microsoft.AspNetCore.Http;
using Roamly.Api.Features.Journal.UploadJournalAttachment;
using Xunit;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalImageMultipartReaderTests
{
    [Fact]
    public async Task ReadAsync_uses_signature_instead_of_client_name_or_content_type()
    {
        var context = CreateContext([0xFF, 0xD8, 0xFF, 0xD9], "text/plain", "not-an-image.txt");

        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await JournalImageMultipartReader.ReadAsync(context, cancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.ContentType);
        await using var stream = result.Stream!;
        using var actual = new MemoryStream();
        await stream.CopyToAsync(actual, cancellationToken);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, actual.ToArray());
    }

    [Fact]
    public async Task ReadAsync_rejects_unsupported_signature()
    {
        var context = CreateContext(Encoding.UTF8.GetBytes("not an image"), "image/png", "photo.png");

        var result = await JournalImageMultipartReader.ReadAsync(
            context, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.True(result.IsUnsupported);
    }

    [Fact]
    public async Task ReadAsync_rejects_additional_multipart_sections()
    {
        var context = CreateContext([0xFF, 0xD8, 0xFF, 0xD9], "image/jpeg", "photo.jpg", appendExtraSection: true);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await JournalImageMultipartReader.ReadAsync(context, cancellationToken);
        Assert.True(result.IsValid);

        await using var stream = result.Stream!;
        await Assert.ThrowsAsync<InvalidDataException>(
            () => stream.CopyToAsync(Stream.Null, cancellationToken));
    }

    [Fact]
    public async Task ReadAsync_accepts_exactly_ten_mibibytes()
    {
        var content = new byte[JournalImageMultipartReader.MaximumBytes];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        var context = CreateContext(content, "application/octet-stream", "image.bin");

        var result = await JournalImageMultipartReader.ReadAsync(
            context, TestContext.Current.CancellationToken);
        Assert.True(result.IsValid);

        await using var stream = result.Stream!;
        using var actual = new MemoryStream();
        await stream.CopyToAsync(actual, TestContext.Current.CancellationToken);
        Assert.Equal(JournalImageMultipartReader.MaximumBytes, actual.Length);
    }

    [Fact]
    public async Task ReadAsync_rejects_more_than_ten_mibibytes_while_streaming()
    {
        var content = new byte[JournalImageMultipartReader.MaximumBytes + 1];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        var context = CreateContext(content, "image/jpeg", "photo.jpg");

        var cancellationToken = TestContext.Current.CancellationToken;
        var result = await JournalImageMultipartReader.ReadAsync(context, cancellationToken);
        Assert.True(result.IsValid);

        await using var stream = result.Stream!;
        await Assert.ThrowsAsync<JournalImageTooLargeException>(
            () => stream.CopyToAsync(Stream.Null, cancellationToken));
    }

    private static DefaultHttpContext CreateContext(
        byte[] content,
        string contentType,
        string filename,
        bool appendExtraSection = false)
    {
        const string boundary = "roamly-journal-test-boundary";
        var body = new MemoryStream();
        var header = Encoding.UTF8.GetBytes(
            $"--{boundary}\r\n"
            + $"Content-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n"
            + $"Content-Type: {contentType}\r\n\r\n");
        body.Write(header);
        body.Write(content);
        body.Write(Encoding.UTF8.GetBytes(
            appendExtraSection
                ? $"\r\n--{boundary}\r\n"
                    + "Content-Disposition: form-data; name=\"extra\"; filename=\"extra.txt\"\r\n"
                    + "Content-Type: text/plain\r\n\r\nextra\r\n"
                    + $"--{boundary}--\r\n"
                : $"\r\n--{boundary}--\r\n"));
        body.Position = 0;

        var context = new DefaultHttpContext();
        context.Request.ContentType = $"multipart/form-data; boundary={boundary}";
        context.Request.Body = body;
        return context;
    }
}
