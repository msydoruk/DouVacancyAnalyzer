namespace DouVacancyAnalyzer.Services;

public interface IAiClient
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}