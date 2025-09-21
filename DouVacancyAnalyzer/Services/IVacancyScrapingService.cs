using DouVacancyAnalyzer.Models;

namespace DouVacancyAnalyzer.Services;

public interface IVacancyScrapingService
{
    Task<List<Vacancy>> GetVacanciesAsync(CancellationToken cancellationToken = default);
    Task<List<Vacancy>> GetTestVacanciesAsync(int limit, CancellationToken cancellationToken = default);
}


