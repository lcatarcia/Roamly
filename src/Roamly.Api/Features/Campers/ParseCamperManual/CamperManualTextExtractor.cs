using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Roamly.Api.Features.Campers.ParseCamperManual;

/// <summary>Pagina di testo estratta da un manuale PDF o da una scheda tecnica.</summary>
public sealed record ExtractedManualPage(int PageNumber, IReadOnlyList<string> Lines);

/// <summary>Estrae il testo selezionabile da flussi PDF o pagine HTML senza dipendenze esterne.</summary>
public static partial class CamperManualTextExtractor
{
    /// <summary>Verifica se il buffer inizia con la firma standard <c>%PDF-</c>.</summary>
    public static bool IsPdf(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 5
            && bytes[0] == (byte)'%'
            && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D'
            && bytes[3] == (byte)'F'
            && bytes[4] == (byte)'-';
    }

    /// <summary>Estrae le pagine testuali da un documento PDF.</summary>
    public static IReadOnlyList<ExtractedManualPage> ExtractFromPdf(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        var latin1 = Encoding.Latin1.GetString(pdfBytes);
        var matches = PdfStreamObjectRegex().Matches(latin1);
        var pages = new List<ExtractedManualPage>();
        var sequentialPage = 1;

        foreach (Match match in matches)
        {
            var dictionary = match.Groups[2].Value;
            var rawStream = match.Groups[3].Value;

            if (dictionary.Contains("/Subtype /Image", StringComparison.Ordinal)
                || dictionary.Contains("/Subtype/Image", StringComparison.Ordinal)
                || dictionary.Contains("/Type /XRef", StringComparison.Ordinal)
                || dictionary.Contains("/Subtype /XML", StringComparison.Ordinal))
            {
                continue;
            }

            var decodedBytes = DecodeStream(dictionary, rawStream);
            if (decodedBytes is null || decodedBytes.Length == 0)
            {
                continue;
            }

            var content = Encoding.Latin1.GetString(decodedBytes);
            var lines = ExtractLinesFromContentStream(content);
            if (lines.Count == 0)
            {
                continue;
            }

            var detectedPage = DetectPageNumber(lines) ?? sequentialPage;
            pages.Add(new ExtractedManualPage(detectedPage, lines));
            sequentialPage = Math.Max(sequentialPage + 1, detectedPage + 1);
        }

        return pages;
    }

    /// <summary>Estrae righe pulite da testo libero o HTML.</summary>
    public static IReadOnlyList<ExtractedManualPage> ExtractFromTextOrHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var normalized = content;
        if (normalized.Contains('<', StringComparison.Ordinal) && normalized.Contains('>', StringComparison.Ordinal))
        {
            normalized = HtmlScriptOrStyleRegex().Replace(normalized, " ");
            normalized = HtmlBlockTagRegex().Replace(normalized, "\n");
            normalized = HtmlAnyTagRegex().Replace(normalized, " ");
            normalized = WebUtility.HtmlDecode(normalized);
        }

        var lines = normalized
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CollapseWhitespace)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return lines.Length == 0
            ? []
            : [new ExtractedManualPage(1, lines)];
    }

    private static byte[]? DecodeStream(string dictionary, string rawStream)
    {
        var data = Encoding.Latin1.GetBytes(rawStream);
        var hasAscii85 = dictionary.Contains("ASCII85Decode", StringComparison.Ordinal)
            || dictionary.Contains("A85", StringComparison.Ordinal);
        var hasFlate = dictionary.Contains("FlateDecode", StringComparison.Ordinal)
            || dictionary.Contains("/Fl", StringComparison.Ordinal);

        if (hasAscii85)
        {
            data = DecodeAscii85(rawStream);
        }

        if (hasFlate)
        {
            return DecompressFlate(data);
        }

        return data;
    }

    private static byte[] DecodeAscii85(string input)
    {
        using var output = new MemoryStream();
        var count = 0;
        uint tuple = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '~')
            {
                break;
            }

            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (ch == 'z' && count == 0)
            {
                output.Write([0, 0, 0, 0], 0, 4);
                continue;
            }

            if (ch is < '!' or > 'u')
            {
                continue;
            }

            tuple = (tuple * 85) + (uint)(ch - '!');
            count++;
            if (count == 5)
            {
                output.WriteByte((byte)(tuple >> 24));
                output.WriteByte((byte)(tuple >> 16));
                output.WriteByte((byte)(tuple >> 8));
                output.WriteByte((byte)tuple);
                tuple = 0;
                count = 0;
            }
        }

        if (count > 1)
        {
            for (var i = count; i < 5; i++)
            {
                tuple = (tuple * 85) + 84;
            }

            for (var i = 0; i < count - 1; i++)
            {
                output.WriteByte((byte)(tuple >> (24 - (i * 8))));
            }
        }

        return output.ToArray();
    }

    private static byte[]? DecompressFlate(byte[] compressed)
    {
        if (compressed.Length == 0)
        {
            return null;
        }

        try
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            try
            {
                using var input = new MemoryStream(compressed);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }

    private static List<string> ExtractLinesFromContentStream(string content)
    {
        var lines = new List<string>();
        var length = content.Length;
        var index = 0;

        while (index < length)
        {
            var ch = content[index];
            if (ch == '(')
            {
                var literal = ReadPdfLiteralString(content, ref index);
                var cleaned = CollapseWhitespace(literal);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    lines.Add(cleaned);
                }

                continue;
            }

            if (ch == '<' && index + 1 < length && content[index + 1] != '<')
            {
                var hex = ReadPdfHexString(content, ref index);
                var cleaned = CollapseWhitespace(hex);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    lines.Add(cleaned);
                }

                continue;
            }

            index++;
        }

        return lines;
    }

    private static string ReadPdfLiteralString(string content, ref int index)
    {
        var builder = new StringBuilder();
        var depth = 0;

        while (index < content.Length)
        {
            var ch = content[index++];
            if (ch == '(')
            {
                if (depth > 0)
                {
                    builder.Append('(');
                }

                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                if (depth <= 0)
                {
                    break;
                }

                builder.Append(')');
                continue;
            }

            if (ch == '\\' && index < content.Length)
            {
                var next = content[index];
                if (next is >= '0' and <= '7')
                {
                    var octal = new StringBuilder(3);
                    while (index < content.Length && octal.Length < 3 && content[index] is >= '0' and <= '7')
                    {
                        octal.Append(content[index++]);
                    }

                    var code = Convert.ToInt32(octal.ToString(), 8);
                    builder.Append(DecodeWinAnsiByte(code));
                    continue;
                }

                index++;
                builder.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'b' => '\b',
                    'f' => '\f',
                    _ => next,
                });
                continue;
            }

            builder.Append(DecodeWinAnsiByte(ch));
        }

        return builder.ToString();
    }

    private static string ReadPdfHexString(string content, ref int index)
    {
        index++; // skip '<'
        var hexChars = new StringBuilder();
        while (index < content.Length && content[index] != '>')
        {
            var ch = content[index++];
            if (Uri.IsHexDigit(ch))
            {
                hexChars.Append(ch);
            }
        }

        if (index < content.Length && content[index] == '>')
        {
            index++;
        }

        if (hexChars.Length == 0)
        {
            return string.Empty;
        }

        if (hexChars.Length % 2 == 1)
        {
            hexChars.Append('0');
        }

        var bytes = new byte[hexChars.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(
                hexChars.ToString(i * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var decoded = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            if (b >= 32)
            {
                decoded.Append(DecodeWinAnsiByte(b));
            }
        }

        return decoded.ToString();
    }

    private static char DecodeWinAnsiByte(int code)
    {
        return code switch
        {
            0x7F => '•',
            0x80 => '€',
            0x91 => '‘',
            0x92 => '’',
            0x93 => '“',
            0x94 => '”',
            0x95 => '•',
            0x96 => '–',
            0x97 => '—',
            _ => (char)code,
        };
    }

    private static int? DetectPageNumber(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = PageIndicatorRegex().Match(line);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
                && page is >= 1 and <= 2000)
            {
                return page;
            }
        }

        return null;
    }

    private static string CollapseWhitespace(string value)
    {
        return MultipleWhitespaceRegex().Replace(value, " ").Trim();
    }

    [GeneratedRegex(@"(\d+)\s+0\s+obj[\s\S]*?<<((?:(?!endobj)[\s\S])*?)>>\s*stream[\r\n]+([\s\S]*?)[\r\n]*endstream")]
    private static partial Regex PdfStreamObjectRegex();

    [GeneratedRegex(@"^(?:Pagina|Page|Pag\.)\s*(\d{1,4})$", RegexOptions.IgnoreCase)]
    private static partial Regex PageIndicatorRegex();

    [GeneratedRegex(@"<(script|style)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlScriptOrStyleRegex();

    [GeneratedRegex(@"</?(?:p|div|tr|td|th|li|ul|ol|h[1-6]|br|section|article|header|footer|table)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlBlockTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlAnyTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespaceRegex();
}
