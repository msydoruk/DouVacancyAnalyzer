using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace DouVacancyAnalyzer.Services;

public class AnthropicAiClient : IAiClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ILogger<AnthropicAiClient> _logger;

    public AnthropicAiClient(string apiKey, string model, ILogger<AnthropicAiClient> logger)
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        var retryDelays = new[] { 2000, 5000, 10000 }; // 2s, 5s, 10s

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var messages = new List<Message>
                {
                    new Message(RoleType.User, userPrompt)
                };

                var parameters = new MessageParameters
                {
                    Messages = messages,
                    Model = _model,
                    MaxTokens = 4096,
                    Stream = false,
                    Temperature = 0.0m,
                    System = new List<SystemMessage> { new SystemMessage(systemPrompt) }
                };

                var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

                if (response.Content != null && response.Content.Count > 0)
                {
                    var textContent = response.Content[0] as Anthropic.SDK.Messaging.TextContent;
                    return textContent?.Text ?? string.Empty;
                }

                _logger.LogWarning("Anthropic API returned empty response");
                return string.Empty;
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("overloaded_error") && attempt < maxRetries)
            {
                var delay = retryDelays[attempt];
                _logger.LogWarning("Anthropic API overloaded, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay, attempt + 1, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Anthropic API");
                throw;
            }
        }

        throw new Exception("Anthropic API overloaded after all retries");
    }
}