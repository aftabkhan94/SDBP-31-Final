using AgentAssignment.Server.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AgentAssignment.Server.Controllers;



[ApiController]
[Produces("application/json")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatCompletionService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

   

    [HttpPost("/v1/chat/completions")]
    [ProducesResponseType<ChatCompletionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteAsync(
        [FromBody] ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Bad input -> 400. The caller must fix their request.
        if (request?.Messages is null || request.Messages.Count == 0)
        {
            return BadRequest(Error(
                "The 'messages' array is required and must contain at least one message.",
                "invalid_request_error"));
        }

 

        if (!_chatService.IsReady)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Error(
                "The model is still starting. Check GET /health and retry once it reports ready.",
                "service_unavailable"));
        }

       

        try
        {
            ChatCompletionResponse response = await _chatService.CompleteAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            // Full details go to the server log only.
            _logger.LogError(ex, "Chat completion failed.");

            return StatusCode(StatusCodes.Status500InternalServerError, Error(
                "An internal error occurred while generating the completion.",
                "server_error"));
        }
    }

   

    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Health()
    {
        return _chatService.IsReady
            ? Ok(new { status = "ready" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "starting" });
    }

    private static ApiErrorResponse Error(string message, string type) =>
        new(new ApiErrorBody(message, type));
}
