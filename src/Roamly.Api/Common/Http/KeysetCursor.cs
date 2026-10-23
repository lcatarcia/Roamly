using System.Text;

namespace Roamly.Api.Common.Http;

/// <summary>Codifica un identificativo COMB come cursore Base64Url opaco a livello di contratto (R52).</summary>
public static class KeysetCursor
{
    /// <summary>Codifica un identificativo in una stringa Base64Url opaca.</summary>
    public static string Encode(Guid id)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString("D")));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Decodifica un cursore solo se contiene un identificativo Guid non vuoto.</summary>
    public static bool TryDecode(string cursor, out Guid id)
    {
        id = default;
        if (cursor.Length != 48 || cursor.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return false;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return Guid.TryParseExact(value, "D", out id)
                && id != Guid.Empty
                && Encode(id) == cursor;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
