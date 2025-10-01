using DouVacancyAnalyzer.Core.Application.DTOs;

namespace DouVacancyAnalyzer.Core.Application.Interfaces;

public interface IVacancyScrapingService
{
    Task<(List<Vacancy> newVacancies, List<string> allVacancyUrls)> GetVacanciesAsync(CancellationToken cancellationToken = default);
}


