using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CardDisputes.Infrastructure;

public readonly record struct CursorPosition(DateTimeOffset OpenedAt, Guid Id);

public sealed class InvalidCursorException : Exception
{
    public InvalidCursorException(string message)
        : base(message)
    {
    }
}

public static class Cursor
{
    public const string SortOpenedAtId = "opened_at_desc,id_desc";

    private const byte Version = 1;
    private const int SignatureLength = 32;

    private static readonly byte[] SigningKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("card-disputes-cursor-signing-key-v1"));

    public static string Encode(CursorPosition position, string sort = SortOpenedAtId)
    {
        ArgumentNullException.ThrowIfNull(sort);

        var micros = position.OpenedAt.ToUniversalTime().UtcTicks / 10;
        var payload = string.Join(
            "\n",
            sort,
            micros.ToString(CultureInfo.InvariantCulture),
            position.Id.ToString("D", CultureInfo.InvariantCulture));

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(SigningKey, payloadBytes);

        var combined = new byte[1 + SignatureLength + payloadBytes.Length];
        combined[0] = Version;
        signature.CopyTo(combined.AsSpan(1, SignatureLength));
        payloadBytes.CopyTo(combined.AsSpan(1 + SignatureLength));

        return Base64UrlEncode(combined);
    }

    public static CursorPosition Decode(string cursor, string sort = SortOpenedAtId)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(sort);

        byte[] raw;
        try
        {
            raw = Base64UrlDecode(cursor);
        }
        catch (FormatException)
        {
            throw new InvalidCursorException("The cursor is not valid base64url.");
        }

        if (raw.Length <= 1 + SignatureLength || raw[0] != Version)
        {
            throw new InvalidCursorException("The cursor is not valid.");
        }

        var signature = raw.AsSpan(1, SignatureLength);
        var payloadBytes = raw.AsSpan(1 + SignatureLength);
        var expected = HMACSHA256.HashData(SigningKey, payloadBytes);

        if (!CryptographicOperations.FixedTimeEquals(signature, expected))
        {
            throw new InvalidCursorException("The cursor failed its integrity check.");
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var parts = payload.Split('\n');

        if (parts.Length != 3)
        {
            throw new InvalidCursorException("The cursor payload is malformed.");
        }

        if (!string.Equals(parts[0], sort, StringComparison.Ordinal))
        {
            throw new InvalidCursorException("The cursor was issued for a different sort order.");
        }

        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var micros)
            || micros < 0
            || !Guid.TryParse(parts[2], out var id))
        {
            throw new InvalidCursorException("The cursor payload is malformed.");
        }

        return new CursorPosition(new DateTimeOffset(micros * 10, TimeSpan.Zero), id);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        padded = (padded.Length % 4) switch
        {
            0 => padded,
            2 => padded + "==",
            3 => padded + "=",
            _ => throw new FormatException("The base64url length is not valid.")
        };

        return Convert.FromBase64String(padded);
    }
}
