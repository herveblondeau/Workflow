using AwesomeAssertions;
using Core.Models;
using Infrastructure.Watermarking;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfStream = Core.Models.PdfStream;

namespace Tests.Infrastructure;

public class PdfWatermarkToolTests
{
    private static PdfStream BuildMinimalPdf()
    {
        var buffer = new MemoryStream();
        using (var doc = new Document())
        {
            PdfWriter.GetInstance(doc, buffer);
            doc.Open();
            doc.Add(new Paragraph("Hello"));
            doc.Close();
        }
        buffer.Position = 0;
        return new PdfStream(buffer);
    }

    [Fact]
    public async Task Transform_Invisible_ReturnsOkWithWatermarkedPdf()
    {
        // Arrange
        var input = BuildMinimalPdf();
        var options = new WatermarkOptions
        {
            Type = WatermarkType.Invisible,
            ContentType = WatermarkContentType.Custom,
            CustomText = "TEST-WATERMARK"
        };
        var sut = new PdfWatermarkTool(options);

        // Act
        var result = await sut.Transform(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var output = new MemoryStream();
        await result.Value.CopyToAsync(output);
        var bytes = output.ToArray();
        bytes.Should().NotBeEmpty();

        using var reader = new PdfReader(bytes);
        reader.Info.Should().ContainKey("Watermark");
        reader.Info["Watermark"].Should().Be("TEST-WATERMARK");
        reader.Info.Should().ContainKey("WatermarkDate");
        reader.Info["WatermarkDate"].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Transform_InvalidPdf_ReturnsFail()
    {
        // Arrange
        var input = new PdfStream(new MemoryStream("not a pdf"u8.ToArray()));
        var options = new WatermarkOptions { Type = WatermarkType.Invisible, ContentType = WatermarkContentType.Timestamp };
        var sut = new PdfWatermarkTool(options);

        // Act
        var result = await sut.Transform(input);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains(nameof(PdfWatermarkTool)));
    }
}
