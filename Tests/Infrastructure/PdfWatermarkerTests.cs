using AwesomeAssertions;
using Infrastructure.Filigrane;
using Infrastructure.Filigrane.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Tests.Infrastructure;

public class PdfWatermarkerTests
{
    [Fact]
    public void Watermark_Invisible_AddsInfoDictionaryEntries()
    {
        // Arrange: build a minimal one-page PDF
        var input = new MemoryStream();
        using (var doc = new Document())
        {
            PdfWriter.GetInstance(doc, input);
            doc.Open();
            doc.Add(new Paragraph("Hello"));
            doc.Close();
        }
        input.Position = 0;

        var output = new MemoryStream();
        var sut = new PdfWatermarker();

        var options = new WatermarkOptions
        {
            Type = WatermarkType.Invisible,
            ContentType = WatermarkContentType.Custom,
            CustomText = "TEST-WATERMARK"
        };

        // Act
        sut.Watermark(input, output, options);

        // Assert
        var bytes = output.ToArray();
        bytes.Should().NotBeEmpty();

        using var reader = new PdfReader(bytes);
        reader.Info.Should().ContainKey("Watermark");
        reader.Info["Watermark"].Should().Be("TEST-WATERMARK");
        reader.Info.Should().ContainKey("WatermarkDate");
        reader.Info["WatermarkDate"].Should().NotBeNullOrWhiteSpace();
    }
}
