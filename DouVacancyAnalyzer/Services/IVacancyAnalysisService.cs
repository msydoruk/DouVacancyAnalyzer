using DouVacancyAnalyzer.Models;

namespace DouVacancyAnalyzer.Services;

public interface IVacancyAnalysisService
{
    Task<VacancyAnalysisResult> AnalyzeVacancyAsync(Vacancy vacancy, CancellationToken cancellationToken = default);
    Task<AnalysisReport> AnalyzeVacanciesAsync(List<Vacancy> vacancies, CancellationToken cancellationToken = default);
    TechnologyStatistics GetTechnologyStatistics(List<Vacancy> vacancies);
    Task<TechnologyStatistics> GetAiTechnologyStatisticsAsync(List<Vacancy> vacancies, CancellationToken cancellationToken = default);
    TechnologyStatistics GetAiTechnologyStatisticsFromAnalyses(List<VacancyAnalysisResult> analyses);
}


