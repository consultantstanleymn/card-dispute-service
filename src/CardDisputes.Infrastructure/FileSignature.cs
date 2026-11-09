namespace CardDisputes.Infrastructure;

public static class FileSignature
{
    public const long MaxFileSizeBytes = 5L * 1024 * 1024;

    public static bool IsSupportedContentType(string? contentType) => contentType switch
    {
        "application/pdf" => true,
        "image/png" => true,
        "image/jpeg" => true,
        _ => false
    };

    public static bool MatchesMagicBytes(string contentType, ReadOnlySpan<byte> bytes)
    {
        switch (contentType)
        {
            case "application/pdf":
                return bytes.Length >= 5
                    && bytes[0] == 0x25
                    && bytes[1] == 0x50
                    && bytes[2] == 0x44
                    && bytes[3] == 0x46
                    && bytes[4] == 0x2D;
            case "image/png":
                return bytes.Length >= 8
                    && bytes[0] == 0x89
                    && bytes[1] == 0x50
                    && bytes[2] == 0x4E
                    && bytes[3] == 0x47
                    && bytes[4] == 0x0D
                    && bytes[5] == 0x0A
                    && bytes[6] == 0x1A
                    && bytes[7] == 0x0A;
            case "image/jpeg":
                return bytes.Length >= 3
                    && bytes[0] == 0xFF
                    && bytes[1] == 0xD8
                    && bytes[2] == 0xFF;
            default:
                return false;
        }
    }
}
