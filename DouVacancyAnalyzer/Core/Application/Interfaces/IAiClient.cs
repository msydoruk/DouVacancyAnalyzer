namespace DouVacancyAnalyzer.Core.Application.Interfaces;

public interface IAiClient
{
    Task<string> CompleteChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}