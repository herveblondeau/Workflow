using Infrastructure.Filigrane.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Infrastructure.Filigrane;

public class PdfWatermarker
{
    public void Watermark(Stream input, Stream output, WatermarkOptions options)
    {
        var watermarkText = options.ContentType == WatermarkContentType.Timestamp
            ? $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            : options.CustomText ?? string.Empty;

        using var reader = new PdfReader(input);
        using var stamper = new PdfStamper(reader, output);

        if (options.Type is WatermarkType.Invisible or WatermarkType.Both)
            AddInvisibleWatermark(stamper, watermarkText);

        if (options.Type is WatermarkType.Visible or WatermarkType.Both)
            AddVisibleWatermark(reader, stamper, watermarkText, options);
    }

    private static void AddInvisibleWatermark(PdfStamper stamper, string text)
    {
        // Embed in the PDF Info dictionary (visible in document properties)
        stamper.MoreInfo["Watermark"] = text;
        stamper.MoreInfo["WatermarkDate"] = DateTimeOffset.UtcNow.ToString("O");

        // Embed a minimal XMP metadata block
        var xmp = $"""
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d""?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
                  <dc:description>{System.Security.SecurityElement.Escape(text)}</dc:description>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            <?xpacket end="w"?>
            """;
        stamper.XmpMetadata = System.Text.Encoding.UTF8.GetBytes(xmp);
    }

    private static void AddVisibleWatermark(PdfReader reader, PdfStamper stamper,
        string text, WatermarkOptions options)
    {
        var baseColor = ParseColor(options.Color);
        var font = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.WINANSI, BaseFont.NOT_EMBEDDED);

        for (int i = 1; i <= reader.NumberOfPages; i++)
        {
            var pageSize = reader.GetPageSizeWithRotation(i);
            var canvas = stamper.GetOverContent(i);

            canvas.SaveState();

            // Set transparency
            var gState = new PdfGState { FillOpacity = options.Opacity, StrokeOpacity = options.Opacity };
            canvas.SetGState(gState);

            canvas.SetColorFill(baseColor);
            canvas.BeginText();
            canvas.SetFontAndSize(font, options.FontSize);

            var (x, y, rotation) = ComputePosition(pageSize, options.Position, options.FontSize, text, font);
            canvas.ShowTextAligned(Element.ALIGN_CENTER, text, x, y, rotation);

            canvas.EndText();
            canvas.RestoreState();
        }
    }

    private static (float x, float y, float rotation) ComputePosition(
        Rectangle page, WatermarkPosition position, int fontSize, string text, BaseFont font)
    {
        float w = page.Width;
        float h = page.Height;
        float textWidth = font.GetWidthPoint(text, fontSize);
        float margin = fontSize * 1.5f;

        return position switch
        {
            WatermarkPosition.Diagonal => (w / 2, h / 2, 45f),
            WatermarkPosition.TopLeft => (margin + textWidth / 2, h - margin, 0f),
            WatermarkPosition.TopCenter => (w / 2, h - margin, 0f),
            WatermarkPosition.TopRight => (w - margin - textWidth / 2, h - margin, 0f),
            WatermarkPosition.Center => (w / 2, h / 2, 0f),
            WatermarkPosition.BottomLeft => (margin + textWidth / 2, margin, 0f),
            WatermarkPosition.BottomCenter => (w / 2, margin, 0f),
            WatermarkPosition.BottomRight => (w - margin - textWidth / 2, margin, 0f),
            _ => (w / 2, h / 2, 45f)
        };
    }

    private static BaseColor ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex[..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);
        return new BaseColor(r, g, b);
    }
}
