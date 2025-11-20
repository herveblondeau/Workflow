using System.Diagnostics;
using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.Tools.Transcribers;

// Transcriber that uses Tesseract OCR to extract text from an image stream
// Requires Tesseract (https://github.com/tesseract-ocr/tesseract?tab=readme-ov-file) to be installed and accessible in PATH
// Language codes supported: "en" (English), "fr" (French), "ja" (Japanese)
// Language files must be downloaded (https://github.com/tesseract-ocr/tessdata). They must placed in the default folder, which depends on the OS (example for Linux: /usr/local/share/tesseract/tessdata/), or anywhere else by specifying a custom folder in the TESSDATA_PREFIX environment variable.
public class TesseractOcrTranscriber : ITool<ImageStream, string>
{
    private readonly string _language;

    public TesseractOcrTranscriber(string language)
    {
        _language = language;
    }

    public async Task<Result<string>> Transform(ImageStream input, CancellationToken cancellationToken = default)
    {
        var tesseractLanguageCode = _convertLanguageToTesseractCode(_language);
        if (tesseractLanguageCode == null)
        {
            return Result.Fail(new Error($"{nameof(TesseractOcrTranscriber)}: language '{_language}' is not supported"));
        }

        var tempImageFile = Path.GetTempFileName();

        try
        {
            using (var fileStream = File.Create(tempImageFile)) // Tesseract is run as an external process and thus requires an actual image file to work
            {
                await input.CopyToAsync(fileStream);
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(TesseractOcrTranscriber)}: cannot create image file {tempImageFile}").CausedBy(ex));
        }

        var tcs = new TaskCompletionSource<(int ExitCode, string SuccessMessage, string ErrorMessage)>();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"\"{tempImageFile}\" - -l {tesseractLanguageCode}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        string stdOutput = "";
        string stdError = "";
        process.OutputDataReceived += (sender, e) => { if (e.Data != null) stdOutput += e.Data + Environment.NewLine; };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) stdError += e.Data + Environment.NewLine; };
        process.Exited += (sender, e) =>
        {
            tcs.SetResult((process.ExitCode, stdOutput, stdError));
            process.Dispose();
        };

        bool started;
        try
        {
            started = process.Start();
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(TesseractOcrTranscriber)}: cannot start Tesseract process").CausedBy(ex));
        }
        if (!started)
        {
            return Result.Fail(new Error($"{nameof(TesseractOcrTranscriber)}: cannot start Tesseract process"));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var task = tcs.Task;
        await task;

        if (task.Result.ExitCode != 0)
        {
            return Result.Fail(new Error($"{nameof(TesseractOcrTranscriber)}: Tesseract process failed with exit code {task.Result.ExitCode}. Error output: {task.Result.ErrorMessage}"));
        }

        return Result.Ok(task.Result.SuccessMessage);
    }

    private string? _convertLanguageToTesseractCode(string language)
    {
        return language switch
        {
            "en" => "eng",
            "fr" => "fra",
            "ja" => "jpn",
            _ => null,
        };
    }
}
