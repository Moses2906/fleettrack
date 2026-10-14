using System.Text;
using FleetTrack.Core.Rules;

namespace FleetTrack.Tests.Rules;

public class DocumentFileRulesTests
{
    [Fact]
    public void Pdf_IsRecognisedByItsContent()
    {
        Assert.Equal("application/pdf", DocumentFileRules.DetectType("%PDF-1.7\n"u8)?.ContentType);
    }

    [Fact]
    public void Png_IsRecognisedByItsContent()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        Assert.Equal("image/png", DocumentFileRules.DetectType(png)?.ContentType);
    }

    [Fact]
    public void Jpeg_IsRecognisedByItsContent()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        Assert.Equal("image/jpeg", DocumentFileRules.DetectType(jpeg)?.ContentType);
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script>")] // a web page renamed to .pdf
    [InlineData("MZ\u0090\0")]                       // a Windows program
    [InlineData("PK\u0003\u0004")]                   // a zip / Word document
    [InlineData("")]
    public void OtherContent_IsRejectedWhateverItIsCalled(string content)
    {
        Assert.Null(DocumentFileRules.DetectType(Encoding.Latin1.GetBytes(content)));
    }

    [Theory]
    [InlineData("rc1.pdf", true)]
    [InlineData("ID.JPG", true)]
    [InlineData("scan.jpeg", true)]
    [InlineData("photo.png", true)]
    [InlineData("rc1.pdf.exe", false)]
    [InlineData("notes.docx", false)]
    [InlineData("noextension", false)]
    public void OnlyPdfJpgAndPngExtensions_AreAllowed(string name, bool allowed)
    {
        Assert.Equal(allowed, DocumentFileRules.HasAllowedExtension(name));
    }

    [Theory]
    [InlineData(@"C:\Users\thabo\Desktop\rc1.pdf", "rc1.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("  ", "document")]
    public void CleanFileName_StripsFoldersAndPathTricks(string input, string expected)
    {
        Assert.Equal(expected, DocumentFileRules.CleanFileName(input));
    }
}
