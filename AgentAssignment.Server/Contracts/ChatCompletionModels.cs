using System.Text.Json.Serialization;

namespace AgentAssignment.Server.Contracts;

// ---------------------------------------------------------------------------
// GIVEN — do not change this file. This is the wire contract your endpoints
// must speak. It mirrors the real OpenAI chat-completions schema closely
// enough that the SAME OpenAIClient code from Phase 2 / M8 (ModelSetup.CreateCloud)
// can point at your server and just work, with no changes on that side.
// ---------------------------------------------------------------------------

public sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
}

public sealed record ChatCompletionChoice
{
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    [JsonPropertyName("message")]
    public required ChatMessage Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public required string FinishReason { get; init; }
}

public sealed record ChatCompletionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }
}

public sealed record ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public string Object { get; init; } = "chat.completion";

    [JsonPropertyName("created")]
    public required long Created { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("choices")]
    public required IReadOnlyList<ChatCompletionChoice> Choices { get; init; }

    [JsonPropertyName("usage")]
    public ChatCompletionUsage? Usage { get; init; }
}

public sealed record ApiErrorBody(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("type")] string Type);

public sealed record ApiErrorResponse(
    [property: JsonPropertyName("error")] ApiErrorBody Error);
