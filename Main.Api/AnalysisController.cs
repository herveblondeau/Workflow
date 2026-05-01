using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Main.Api.Models;
using Infrastructure.ChatAgents;
using Infrastructure.TextTransformers;
using Core.Tools.Workflow;
using Core;
using System.Text.Json;
using Infrastructure.Files;
using Infrastructure.Tools.Transcribers;
using Infrastructure.Downloaders;
using Core.Models;
using Whisper.net.Ggml;
using Infrastructure.Transcribers;
using FluentResults;

namespace Main.Api
{
    [Route("api/analysis")]
    [ApiController]
    // TODO: make tools injectable
    public class AnalysisController : ControllerBase
    {
        private readonly IChatClientFactory _chatClientFactory;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AnalysisController(IChatClientFactory chatClientFactory)
        {
            _chatClientFactory = chatClientFactory;
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetStatus()
        {
            return NoContent();
        }

        [HttpPost("text")]
        public async Task<IActionResult> TransformText([FromBody] TextTransformRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text is required." });
            }

            // Use default language if not provided or auto
            var language = request.Language ?? "en";

            // Prepare instructions - split by newlines if multiple instructions provided
            var instructions = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Instructions))
            {
                // Split by newline and filter out empty lines
                instructions.AddRange(
                    request.Instructions
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Trim())
                        .Where(i => !string.IsNullOrEmpty(i))
                );
            }

            // If no instructions provided, use a default
            if (instructions.Count == 0)
            {
                instructions.Add("Please analyze the provided text and provide a summary of the main points.");
            }

            try
            {
                var chatClient = _chatClientFactory.Create(request.Provider, request.Model);

                var workflow = Workflow
                    .Add(new AITextTransformer(new ChatAgent(chatClient), language, instructions));

                var result = await workflow.Execute(request.Text, cancellationToken);

                if (result.IsFailed)
                {
                    var errors = result.Errors.Select(e => e.Message).ToList();
                    return StatusCode(500, new { error = "Text transformation failed.", details = errors });
                }

                return Ok(new { success = result.IsSuccess, result = result.Value });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while processing the request.", details = ex.Message });
            }
        }

        [HttpPost("image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> TransformImage([FromForm] IFormFile image, [FromForm] string metadata, CancellationToken cancellationToken)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { error = "Image file is required." });
            }

            if (string.IsNullOrWhiteSpace(metadata))
            {
                return BadRequest(new { error = "Metadata is required." });
            }

            ImageTransformMetadata? metadataObj;
            try
            {
                metadataObj = JsonSerializer.Deserialize<ImageTransformMetadata>(metadata, _jsonOptions);
                if (metadataObj == null)
                {
                    return BadRequest(new { error = "Invalid metadata format." });
                }
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = "Failed to parse metadata JSON.", details = ex.Message });
            }

            // Use default language if not provided or auto
            var language = metadataObj.Language ?? "en";

            // Prepare instructions - split by newlines if multiple instructions provided
            var instructions = new List<string>();
            if (!string.IsNullOrWhiteSpace(metadataObj.Instructions))
            {
                // Split by newline and filter out empty lines
                instructions.AddRange(
                    metadataObj.Instructions
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Trim())
                        .Where(i => !string.IsNullOrEmpty(i))
                );
            }

            // If no instructions provided, use a default
            if (instructions.Count == 0)
            {
                instructions.Add("This is a text extracted from an image using OCR.");
                instructions.Add("Please analyze the content and provide a summary of the main points.");
            }

            // Save uploaded file to temporary location
            var tempImagePath = Path.GetTempFileName();
            // Change extension to match image file
            var extension = Path.GetExtension(image.FileName);
            if (!string.IsNullOrEmpty(extension))
            {
                tempImagePath = Path.ChangeExtension(tempImagePath, extension);
            }

            try
            {
                // Save the uploaded file
                using (var fileStream = new FileStream(tempImagePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream, cancellationToken);
                }

                var chatClient = _chatClientFactory.Create(metadataObj.Provider, metadataObj.Model);

                var workflow = Workflow
                    .Add(new ImageFileReader())
                    .Add(new TesseractOcrTranscriber(language))
                    .Add(new AITextTransformer(new ChatAgent(chatClient), language, instructions));

                var result = await workflow.Execute(tempImagePath, cancellationToken);

                if (result.IsFailed)
                {
                    var errors = result.Errors.Select(e => e.Message).ToList();
                    return StatusCode(500, new { error = "Image processing failed.", details = errors });
                }

                return Ok(new { success = result.IsSuccess, result = result.Value });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while processing the request.", details = ex.Message });
            }
            finally
            {
                // Clean up temporary file
                try
                {
                    if (System.IO.File.Exists(tempImagePath))
                    {
                        System.IO.File.Delete(tempImagePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [HttpPost("url")]
        public async Task<IActionResult> TransformUrl([FromBody] URLTransformRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "URL is required." });
            }

            // Use default language if not provided or auto
            var language = request.Language ?? "en";

            // Prepare instructions - split by newlines if multiple instructions provided
            var instructions = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Instructions))
            {
                // Split by newline and filter out empty lines
                instructions.AddRange(
                    request.Instructions
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Trim())
                        .Where(i => !string.IsNullOrEmpty(i))
                );
            }

            try
            {
                var chatClient = _chatClientFactory.Create(request.Provider, request.Model);

                Result<string> result;

                // Check if URL contains "youtube" (case-insensitive)
                if (request.Text.Contains("youtube", StringComparison.OrdinalIgnoreCase))
                {
                    // Use YouTube workflow similar to YouTubeSummary
                    var audioFormat = new AudioFormat(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);
                    var transcriberModel = GgmlType.Base;

                    // Prepare instructions for YouTube video
                    var youtubeInstructions = new List<string>
                    {
                        "This is a transcription of a Youtube video"
                    };
                    if (instructions.Count > 0)
                    {
                        youtubeInstructions.AddRange(instructions);
                    }
                    else
                    {
                        youtubeInstructions.Add("Can you write a summary?");
                    }

                    var workflow = Workflow
                        .Add(FirstSuccessfulTool
                            .Add(new YouTubeSubtitlesDownloader(language))
                            .Add(SequentialTool
                                .Add(new YouTubeAudioDownloader(audioFormat))
                                .Add(new WhisperTranscriber(audioFormat, language, transcriberModel))
                            )
                        )
                        .Add(new AITextTransformer(new ChatAgent(chatClient), language, youtubeInstructions));

                    result = await workflow.Execute(request.Text, cancellationToken);
                }
                else
                {
                    // Use URLDownloader for other URLs
                    if (instructions.Count == 0)
                    {
                        instructions.Add("Please analyze the content from this URL and provide a summary of the main points.");
                    }

                    var workflow = Workflow
                        .Add(new URLDownloader())
                        .Add(new AITextTransformer(new ChatAgent(chatClient), language, instructions));

                    result = await workflow.Execute(request.Text, cancellationToken);
                }

                if (result.IsFailed)
                {
                    var errors = result.Errors.Select(e => e.Message).ToList();
                    return StatusCode(500, new { error = "URL processing failed.", details = errors });
                }

                return Ok(new { success = result.IsSuccess, result = result.Value });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while processing the request.", details = ex.Message });
            }
        }
    }
}
