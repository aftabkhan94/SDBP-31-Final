namespace AgentAssignment.Server.Contracts;

// ---------------------------------------------------------------------------
// GIVEN — the offline test suite registers this instead of
// FoundryLocalChatCompletionService, so grading needs no model, no GPU and
// no network. It answers instantly by echoing the last user message back,
// wrapped in a valid completion. Students never see or touch this file
// directly; it ships inside the test project.
// ---------------------------------------------------------------------------

public sealed class FakeChatCompletionService : IChatCompletionService
{
    /// <summary>Settable so tests can exercise the not-ready path.</summary>
    public bool IsReady { get; set; } = true;

    public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsReady)
            throw new InvalidOperationException("The fake service was set to not-ready for this test.");

        var lastUser = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        var response = new ChatCompletionResponse
        {
            Id      = "chatcmpl-fake-0001",
            Created = 1_700_000_000,
            Model   = request.Model,
            Choices = [ new ChatCompletionChoice
            {
                Index        = 0,
                Message      = new ChatMessage("assistant", $"You said: {lastUser}"),
                FinishReason = "stop"
            }],
            Usage = new ChatCompletionUsage { PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2 }
        };

        return Task.FromResult(response);
    }
}
