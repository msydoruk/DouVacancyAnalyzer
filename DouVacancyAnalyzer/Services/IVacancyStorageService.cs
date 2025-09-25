using DouVacancyAnalyzer.Models;

namespace DouVacancyAnalyzer.Services;

public interface IVacancyStorageService
{
    Task<List<VacancyEntity>> GetAllVacanciesAsync();
    Task<List<VacancyEntity>> GetNewVacanciesAsync();
    Task<VacancyEntity?> GetVacancyByUrlAsync(string url);
    Task<List<VacancyEntity>> SaveVacanciesAsync(List<Vacancy> vacancies);
    Task UpdateVacancyAnalysisAsync(int vacancyId, VacancyAnalysisResult analysis);
    Task MarkVacanciesAsViewedAsync();
    Task ClearDatabaseAsync();
    Task<int> GetTotalVacancyCountAsync();
    Task<int> GetNewVacancyCountAsync();
    Task<List<VacancyEntity>> GetVacanciesWithAnalysisAsync();
    Task<List<VacancyEntity>> GetUnanalyzedVacanciesAsync();
    Task RecalculateContentHashesAsync();
}