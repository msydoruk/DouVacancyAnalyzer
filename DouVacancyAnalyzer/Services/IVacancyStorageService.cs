using DouVacancyAnalyzer.Models;

namespace DouVacancyAnalyzer.Services;

public interface IVacancyStorageService
{
    Task<List<VacancyEntity>> GetAllVacanciesAsync();
    Task<List<VacancyEntity>> GetNewVacanciesAsync();
    Task<VacancyEntity?> GetVacancyByUrlAsync(string url);
    Task<List<VacancyEntity>> SaveVacanciesAsync(List<Vacancy> vacancies, bool markOthersAsInactive = true);
    Task UpdateVacancyAnalysisAsync(int vacancyId, VacancyAnalysisResult analysis);
    Task MarkVacanciesAsViewedAsync();
    Task ClearDatabaseAsync();
    Task<int> GetTotalVacancyCountAsync();
    Task<int> GetNewVacancyCountAsync();
    Task<List<VacancyEntity>> GetVacanciesWithAnalysisAsync();
    Task<List<VacancyEntity>> GetUnanalyzedVacanciesAsync();
    Task<VacancyCountHistory> CreateVacancyCountHistoryAsync(int totalVacancies, int activeVacancies, int newVacancies, int deactivatedVacancies, int matchingVacancies, decimal matchPercentage);
    Task<List<VacancyCountHistory>> GetVacancyCountHistoryAsync(int limit = 30);
    Task<List<VacancyEntity>> GetActiveVacanciesAsync();
    Task<int> GetActiveVacancyCountAsync();
}