using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Main.Api.Models;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.OpenRouter;
using Infrastructure.TextTransformers;
using Core.Tools.Workflow;
using Microsoft.Extensions.Configuration;
using Core;

namespace Main.Api
{
    [Route("api/analysis")]
    [ApiController]
    public class AnalysisController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AnalysisController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // [HttpGet("status")]
        // public IActionResult GetStatus()
        // {
        //     return Ok(new { status = "Analysis service is running." });
        // }

        [HttpPost("text")]
        public async Task<IActionResult> TransformText([FromBody] TextTransformRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text is required." });
            }

            var openRouterApiKey = _configuration["ChatClients:OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(openRouterApiKey))
            {
                return StatusCode(500, new { error = "OpenRouter API key is not configured." });
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
                var chatClient = new OpenRouterChatClient(openRouterApiKey);
                chatClient.UseModel("google/gemini-2.5-flash-image");

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
    }
}
