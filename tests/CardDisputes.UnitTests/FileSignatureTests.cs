#pragma warning disable CA1707
using CardDisputes.Infrastructure;
using Xunit;

namespace CardDisputes.UnitTests;

public sealed class FileSignatureTests
{
    [Theory]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    public void Pdf_Png_Jpeg_Signatures_Accepted(string contentType, byte[] bytes)
    {
        Assert.True(FileSignature.IsSupportedContentType(contentType));
        Assert.True(FileSignature.MatchesMagicBytes(contentType, bytes));
    }

    [Fact]
    public void Exe_Renamed_To_Pdf_Rejected()
    {
        byte[] exeBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];

        Assert.False(FileSignature.MatchesMagicBytes("application/pdf", exeBytes));
    }
}
