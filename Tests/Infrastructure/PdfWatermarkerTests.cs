using AwesomeAssertions;
using Infrastructure.Tools.Watermarking;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Tests.Infrastructure;

public class PdfWatermarkerTests
{
    [Fact]
    public void Watermark_Invisible_AddsExpectedMetadata()
    {
        // Arrange
        var inputPdf = CreatePdfWithText("hello");
        var outputPdf = new MemoryStream();
        var sut = new PdfWatermarker();

        var options = new WatermarkOptions
        {
            Type = WatermarkType.Invisible,
            ContentType = WatermarkContentType.Custom,
            CustomText = "test-watermark"
        };

        // Act
        sut.Watermark(inputPdf, outputPdf, options);

        // Assert
        var bytes = outputPdf.ToArray();
        bytes.Should().NotBeEmpty();
        bytes.Take(4).Should().Equal("%PDF"u8.ToArray());

        using var reader = new PdfReader(bytes);
        reader.Info.Should().ContainKey("Watermark");
        reader.Info["Watermark"].Should().Be("test-watermark");
        reader.Info.Should().ContainKey("WatermarkDate");
        reader.Info["WatermarkDate"].Should().NotBeNullOrWhiteSpace();
    }

    private static MemoryStream CreatePdfWithText(string text)
    {
        var ms = new MemoryStream();
        using var document = new Document(PageSize.A4);
        var writer = PdfWriter.GetInstance(document, ms);
        writer.CloseStream = false;

        document.Open();
        document.Add(new Paragraph(text));
        document.Close();

        ms.Position = 0;
        return ms;
    }
}
