using DouVacancyAnalyzer.Core.Application.DTOs;

namespace DouVacancyAnalyzer.Core.Application.Interfaces;

public interface IVacancyScrapingService
{
    Task<List<Vacancy>> GetVacanciesAsync(CancellationToken cancellationToken = default);
}


