using System.Security.Cryptography;

namespace UrlShortener.Domain.Services;

/// <summary>
/// Cryptographically secure Base62 short-code generator.
/// Uniqueness is enforced by the database; callers retry on collision.
/// </summary>
public sealed class ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate(int length = 8)
    {
        if (length is < 4 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 4 and 32.");
        }

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }

    public static bool IsValidFormat(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode) || shortCode.Length is < 3 or > 64)
        {
            return false;
        }

        foreach (var c in shortCode)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }
}
