// SAMPLE — shows the shape of the Phase 1 offline grading suite: every test here runs
// against FakeChatCompletionService, never a real model. No GPU, no network, no download.
// This is what "plumbing-only" grading looks like (per our discussion): routing, request/
// response schema, status codes, config binding, health — never asserting on model output.
// The final AgentAssignment.Tests project will have more cases in this style (roughly 12-15).

using System.Net;
using System.Net.Http.Json;
using System.Text;
using AgentAssignment.Server.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AgentAssignment.Tests;

public sealed class ServerPlumbingTests : IClassFixture<ServerPlumbingTests.Factory>
{
    private readonly Factory _factory;

    public ServerPlumbingTests(Factory factory)
    {
        _factory = factory;
        _factory.Fake.IsReady = true; // each test starts from a known state
    }

    [Fact]
    public async Task Health_reports_ready_once_the_service_is_ready()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Health_reports_503_before_the_service_is_ready()
    {
        _factory.Fake.IsReady = false;
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task A_valid_request_returns_200_with_an_OpenAI_shaped_body()
    {
        var client = _factory.CreateClient();
        var request = new ChatCompletionRequest
        {
            Model = "local",
            Messages = [new ChatMessage("user", "hello there")]
        };

        var res = await client.PostAsJsonAsync("/v1/chat/completions", request);
        var body = await res.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("chat.completion", body!.Object);
        Assert.Single(body.Choices);
        Assert.Equal("assistant", body.Choices[0].Message.Role);
        Assert.Contains("hello there", body.Choices[0].Message.Content);
    }

    [Fact]
    public async Task The_last_user_message_is_the_one_that_gets_answered()
    {
        var client = _factory.CreateClient();
        var request = new ChatCompletionRequest
        {
            Model = "local",
            Messages =
            [
                new ChatMessage("system", "be nice"),
                new ChatMessage("user", "first question"),
                new ChatMessage("assistant", "first answer"),
                new ChatMessage("user", "second question")
            ]
        };

        var res = await client.PostAsJsonAsync("/v1/chat/completions", request);
        var body = await res.Content.ReadFromJsonAsync<ChatCompletionResponse>();

        Assert.Contains("second question", body!.Choices[0].Message.Content);
        Assert.DoesNotContain("first question", body.Choices[0].Message.Content);
    }

    [Fact]
    public async Task An_empty_messages_array_is_rejected_with_400_and_an_error_body()
    {
        var client = _factory.CreateClient();
        var request = new ChatCompletionRequest { Model = "local", Messages = [] };

        var res = await client.PostAsJsonAsync("/v1/chat/completions", request);
        var body = await res.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body?.Error.Message));
    }

    [Fact]
    public async Task Malformed_JSON_is_rejected_with_400_not_a_500_or_a_hang()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsync("/v1/chat/completions",
            new StringContent("{ this is not valid json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Not_ready_yet_returns_503_not_a_500()
    {
        _factory.Fake.IsReady = false;
        var client = _factory.CreateClient();
        var request = new ChatCompletionRequest { Model = "local", Messages = [new ChatMessage("user", "hi")] };

        var res = await client.PostAsJsonAsync("/v1/chat/completions", request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task Response_content_type_is_json()
    {
        var client = _factory.CreateClient();
        var request = new ChatCompletionRequest { Model = "local", Messages = [new ChatMessage("user", "hi")] };

        var res = await client.PostAsJsonAsync("/v1/chat/completions", request);

        Assert.StartsWith("application/json", res.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Boots the server in-process with the Fake service swapped in — no port, no model.</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeChatCompletionService Fake { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatCompletionService>();
                services.AddSingleton<IChatCompletionService>(Fake);
            });
        }
    }
}
