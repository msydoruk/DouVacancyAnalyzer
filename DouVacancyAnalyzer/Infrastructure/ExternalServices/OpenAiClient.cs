using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using DouVacancyAnalyzer.Core.Application.Interfaces;

namespace DouVacancyAnalyzer.Infrastructure.ExternalServices;

public class OpenAiClient : IAiClient
{
    private readonly OpenAIClient _client;
    private readonly string _model;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(OpenAIClient client, string model, ILogger<OpenAiClient> logger)
    {
        _client = client;
        _model = model;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_model);
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(systemPrompt),
                ChatMessage.CreateUserMessage(userPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            throw;
        }
    }
}