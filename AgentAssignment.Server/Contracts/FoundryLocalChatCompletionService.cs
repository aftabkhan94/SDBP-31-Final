using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using System.ClientModel;

namespace AgentAssignment.Server.Contracts;

// ---------------------------------------------------------------------------
// GIVEN — do not change this file. Starts a small model on this computer
// (via Foundry Local) the first time EnsureStartedAsync is called, and
// answers every request after that. This is the same mechanism as the M8
// assignment's LocalFoundry.cs, just wrapped behind IChatCompletionService
// instead of handed to you as an IChatClient.
//
// Program.cs's job is ONLY to expose this over HTTP. It should register this
// class in DI, call EnsureStartedAsync() in the background at startup
// (don't await it before the server starts listening), and use IsReady to
// decide whether /health and POST /v1/chat/completions can serve traffic yet.
// ---------------------------------------------------------------------------

public sealed class FoundryLocalChatCompletionService : IChatCompletionService, IAsyncDisposable
{
    public const string DefaultAlias = "qwen2.5-0.5b";
    private const string LocalUrl = "http://127.0.0.1:5273";

    private readonly string _alias;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private IChatClient? _model;
    private string? _modelId;

    public FoundryLocalChatCompletionService(string? alias = null) => _alias = alias ?? DefaultAlias;

    public bool IsReady => _model is not null;

    /// <summary>Idempotent — safe to call once at startup. Takes a while the first time (model download).</summary>
    public async Task EnsureStartedAsync()
    {
        if (_model is not null) return;
        await _startLock.WaitAsync();
        try
        {
            if (_model is not null) return;

            await FoundryLocalManager.CreateAsync(new Configuration
            {
                AppName  = "sdbp031-genai-phase1",
              //  LogLevel = LogLevel.Warning,
                Web      = new Configuration.WebService { Urls = LocalUrl }
            }, NullLogger.Instance);

            var manager = FoundryLocalManager.Instance;
            await manager.DownloadAndRegisterEpsAsync((_, _) => { });

            var catalog = await manager.GetCatalogAsync();
            var model = await catalog.GetModelAsync(_alias)
                ?? throw new InvalidOperationException($"'{_alias}' isn't in the Foundry Local catalog. Run 'foundry model list' to see the aliases.");

            await model.DownloadAsync(_ => { });
            await model.LoadAsync();
            await manager.StartWebServiceAsync();

            _modelId = model.Id;
            _model = new OpenAIClient(new ApiKeyCredential("notneeded"),
                    new OpenAIClientOptions { Endpoint = new Uri($"{LocalUrl}/v1/") })
                .GetChatClient(model.Id)
                .AsIChatClient();
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (_model is null)
            throw new InvalidOperationException("The local model has not finished starting yet.");

        var messages = request.Messages
            .Select(m => new Microsoft.Extensions.AI.ChatMessage(new ChatRole(m.Role), m.Content))
            .ToList();

        Microsoft.Extensions.AI.ChatResponse response = await _model.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var text = response.Text ?? "";

        return new ChatCompletionResponse
        {
            Id      = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model   = _modelId ?? request.Model,
            Choices = [ new ChatCompletionChoice
            {
                Index        = 0,
                Message      = new ChatMessage("assistant", text),
                FinishReason = "stop"
            }],
            Usage = new ChatCompletionUsage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0 }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (!FoundryLocalManager.IsInitialized) return;
        await FoundryLocalManager.Instance.StopWebServiceAsync();
        FoundryLocalManager.Instance.Dispose();
    }
}
