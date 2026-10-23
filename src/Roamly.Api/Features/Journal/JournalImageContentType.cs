namespace Roamly.Api.Features.Journal;

/// <summary>Riconosce soltanto le firme binarie approvate; non consulta nome o Content-Type client.</summary>
public static class JournalImageContentType
{
    public static string? FromSignature(ReadOnlySpan<byte> signature)
    {
        if (signature.Length >= 3
            && signature[0] == 0xFF
            && signature[1] == 0xD8
            && signature[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (signature.Length >= 8
            && signature[0] == 0x89
            && signature[1] == 0x50
            && signature[2] == 0x4E
            && signature[3] == 0x47
            && signature[4] == 0x0D
            && signature[5] == 0x0A
            && signature[6] == 0x1A
            && signature[7] == 0x0A)
        {
            return "image/png";
        }

        if (signature.Length >= 12
            && signature[..4].SequenceEqual("RIFF"u8)
            && signature[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
