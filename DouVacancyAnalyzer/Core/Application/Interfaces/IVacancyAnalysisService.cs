using DouVacancyAnalyzer.Core.Application.DTOs;

namespace DouVacancyAnalyzer.Core.Application.Interfaces;

public interface IVacancyAnalysisService
{
    Task<VacancyAnalysisResult> AnalyzeVacancyAsync(Vacancy vacancy, CancellationToken cancellationToken = default);
    TechnologyStatistics GetAiTechnologyStatisticsFromAnalyses(List<VacancyAnalysisResult> analyses);
    TechnologyStatistics GetAiTechnologyStatisticsFromAnalysesWithVacancies(List<VacancyAnalysisResult> analyses, List<VacancyMatch> allMatches);
    Task<(AnalysisReport report, List<VacancyAnalysisResult> allAnalyses, List<VacancyMatch> allMatches)> AnalyzeVacanciesAsync(
        List<Vacancy> vacancies,
        Func<string, int, Task> progressCallback,
        CancellationToken cancellationToken = default);
}


