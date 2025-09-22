using DouVacancyAnalyzer.Models;

namespace DouVacancyAnalyzer.Services;

public interface IVacancyAnalysisService
{
    Task<VacancyAnalysisResult> AnalyzeVacancyAsync(Vacancy vacancy, CancellationToken cancellationToken = default);
    TechnologyStatistics GetTechnologyStatistics(List<Vacancy> vacancies);
    TechnologyStatistics GetAiTechnologyStatisticsFromAnalyses(List<VacancyAnalysisResult> analyses);
    TechnologyStatistics GetAiTechnologyStatisticsFromAnalysesWithVacancies(List<VacancyAnalysisResult> analyses, List<VacancyMatch> allMatches);
    Task<(AnalysisReport report, List<VacancyAnalysisResult> allAnalyses, List<VacancyMatch> allMatches)> AnalyzeVacanciesWithProgressAsync(
        List<Vacancy> vacancies,
        Func<string, int, Task> progressCallback,
        CancellationToken cancellationToken = default);
}


