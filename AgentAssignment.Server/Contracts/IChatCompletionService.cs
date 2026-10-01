namespace AgentAssignment.Server.Contracts;

// ---------------------------------------------------------------------------
// GIVEN — do not change this file. This is the contract your Program.cs is
// graded against. It wraps whatever actually produces a completion — a real
// local model, or (in tests) a fake. Program.cs's only job is to expose this
// over HTTP correctly; it must never talk to Foundry Local directly.
// ---------------------------------------------------------------------------

public interface IChatCompletionService
{
    /// <summary>True once the underlying model is loaded and able to answer. Back GET /health with this.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Produce a completion for the given request. Throws <see cref="InvalidOperationException"/>
    /// if called while <see cref="IsReady"/> is false — Program.cs should check IsReady itself
    /// and return a 503 before calling this, rather than relying on this throwing.
    /// </summary>
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}
