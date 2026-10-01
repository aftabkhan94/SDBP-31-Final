using AgentAssignment.Server.Contracts;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

const int DefaultPort = 5080;
var port = builder.Configuration.GetValue<int?>("Server:Port") ?? DefaultPort;

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(port));



var modelAlias = builder.Configuration["Server:ModelAlias"];
if (string.IsNullOrWhiteSpace(modelAlias))
{
    modelAlias = FoundryLocalChatCompletionService.DefaultAlias;
}

builder.Services.AddSingleton<IChatCompletionService>(
    _ => new FoundryLocalChatCompletionService(modelAlias));

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        

        options.InvalidModelStateResponseFactory = context =>
        {
            

            var details = context.ModelState
                .Where(entry => entry.Key != "request" && entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? $"'{entry.Key}' is invalid." : error.ErrorMessage))
                .ToList();

            var message = details.Count > 0
                ? "The request body is invalid: " + string.Join(" ", details)
                : "The request body is invalid.";

            return new BadRequestObjectResult(
                new ApiErrorResponse(new ApiErrorBody(message, "invalid_request_error")));
        };
    });

var app = builder.Build();




app.UseAuthorization();

app.MapControllers();



if (app.Services.GetRequiredService<IChatCompletionService>() is FoundryLocalChatCompletionService foundry)
{
    _ = Task.Run(async () =>
    {
        try
        {
            app.Logger.LogInformation("Starting local model '{Alias}' in the background...", modelAlias);
            await foundry.EnsureStartedAsync();
            app.Logger.LogInformation("Local model '{Alias}' is ready.", modelAlias);
        }
        catch (Exception ex)
        {
            // Never crash the server; /health simply keeps reporting 503.
            app.Logger.LogError(ex, "The local model '{Alias}' failed to start.", modelAlias);
        }
    });
}

app.Logger.LogInformation("AgentAssignment.Server listening on http://localhost:{Port}", port);

app.Run();



public partial class Program { }
