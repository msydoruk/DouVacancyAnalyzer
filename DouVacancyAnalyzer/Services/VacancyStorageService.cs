using DouVacancyAnalyzer.Data;
using DouVacancyAnalyzer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DouVacancyAnalyzer.Services;

public class VacancyStorageService : IVacancyStorageService
{
    private readonly VacancyDbContext _context;
    private readonly ILogger<VacancyStorageService> _logger;

    public VacancyStorageService(VacancyDbContext context, ILogger<VacancyStorageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<VacancyEntity>> GetAllVacanciesAsync()
    {
        return await _context.Vacancies
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<VacancyEntity>> GetNewVacanciesAsync()
    {
        return await _context.Vacancies
            .Where(v => v.IsNew)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<VacancyEntity?> GetVacancyByUrlAsync(string url)
    {
        return await _context.Vacancies
            .FirstOrDefaultAsync(v => v.Url == url);
    }

    public async Task<List<VacancyEntity>> SaveVacanciesAsync(List<Vacancy> vacancies)
    {
        var savedVacancies = new List<VacancyEntity>();
        var newVacancyCount = 0;

        foreach (var vacancy in vacancies)
        {
            var entity = VacancyEntity.FromVacancy(vacancy);

            _logger.LogInformation("Processing vacancy: {Title} | URL: {Url}",
                vacancy.Title, vacancy.Url);

            var existing = await GetVacancyByUrlAsync(vacancy.Url);
            _logger.LogInformation("URL lookup result: {Found}",
                existing != null ? "FOUND" : "NOT FOUND");

            if (existing == null)
            {
                try
                {
                    _context.Vacancies.Add(entity);
                    await _context.SaveChangesAsync(); // Save immediately
                    savedVacancies.Add(entity);
                    newVacancyCount++;
                    _logger.LogInformation("✅ NEW vacancy saved: {Title} at {Company}",
                        vacancy.Title, vacancy.Company);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving new vacancy: {Title} | URL: {Url}",
                        vacancy.Title, vacancy.Url);

                    // Remove from context to prevent issues
                    _context.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

                    // Try to get existing again in case it was added in parallel
                    var existingRetry = await GetVacancyByUrlAsync(vacancy.Url);
                    if (existingRetry != null)
                    {
                        _logger.LogWarning("Found existing vacancy on retry: {Title} | ID: {Id}",
                            vacancy.Title, existingRetry.Id);
                        existingRetry.PublishedDate = vacancy.PublishedDate;
                        existingRetry.Salary = vacancy.Salary;
                        existingRetry.IsRemote = vacancy.IsRemote;
                        existingRetry.IsNew = false;
                        await _context.SaveChangesAsync();
                        savedVacancies.Add(existingRetry);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                // Update existing vacancy fields that might change
                existing.PublishedDate = vacancy.PublishedDate;
                existing.Salary = vacancy.Salary;
                existing.IsRemote = vacancy.IsRemote;
                existing.IsNew = false; // Mark as not new since it already existed
                await _context.SaveChangesAsync();
                savedVacancies.Add(existing);
                _logger.LogInformation("🔄 EXISTING vacancy updated: {Title} at {Company} | ID: {Id}",
                    vacancy.Title, vacancy.Company, existing.Id);
            }
        }

        _logger.LogInformation("Processed {TotalCount} vacancies: {NewCount} new, {ExistingCount} existing",
            vacancies.Count, newVacancyCount, vacancies.Count - newVacancyCount);

        return savedVacancies;
    }

    public async Task UpdateVacancyAnalysisAsync(int vacancyId, VacancyAnalysisResult analysis)
    {
        var vacancy = await _context.Vacancies.FindAsync(vacancyId);
        if (vacancy == null) return;

        vacancy.VacancyCategory = analysis.VacancyCategory;
        vacancy.DetectedExperienceLevel = analysis.DetectedExperienceLevel;
        vacancy.DetectedEnglishLevel = analysis.DetectedEnglishLevel;
        vacancy.IsModernStack = analysis.IsModernStack;
        vacancy.IsMiddleLevel = analysis.IsMiddleLevel;
        vacancy.HasAcceptableEnglish = analysis.HasAcceptableEnglish;
        vacancy.HasNoTimeTracker = analysis.HasNoTimeTracker;
        vacancy.IsBackendSuitable = analysis.IsBackendSuitable;
        vacancy.AnalysisReason = analysis.AnalysisReason;
        vacancy.MatchScore = (int?)analysis.MatchScore;
        vacancy.DetectedTechnologies = JsonSerializer.Serialize(analysis.DetectedTechnologies ?? new List<string>());
        vacancy.LastAnalyzedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task MarkVacanciesAsViewedAsync()
    {
        var newVacancies = await _context.Vacancies
            .Where(v => v.IsNew)
            .ToListAsync();

        foreach (var vacancy in newVacancies)
        {
            vacancy.IsNew = false;
        }

        if (newVacancies.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} vacancies as viewed", newVacancies.Count);
        }
    }

    public async Task ClearDatabaseAsync()
    {
        var count = await _context.Vacancies.CountAsync();
        _context.Vacancies.RemoveRange(_context.Vacancies);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cleared {Count} vacancies from database", count);
    }

    public async Task<int> GetTotalVacancyCountAsync()
    {
        return await _context.Vacancies.CountAsync();
    }

    public async Task<int> GetNewVacancyCountAsync()
    {
        return await _context.Vacancies.CountAsync(v => v.IsNew);
    }

    public async Task<List<VacancyEntity>> GetVacanciesWithAnalysisAsync()
    {
        return await _context.Vacancies
            .Where(v => v.LastAnalyzedAt != null)
            .OrderByDescending(v => v.MatchScore)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<VacancyEntity>> GetUnanalyzedVacanciesAsync()
    {
        return await _context.Vacancies
            .Where(v => v.LastAnalyzedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task RecalculateContentHashesAsync()
    {
        var allVacancies = await _context.Vacancies.ToListAsync();
        _logger.LogInformation("Recalculating content hashes for {Count} vacancies", allVacancies.Count);

        foreach (var vacancy in allVacancies)
        {
            var vacancyModel = vacancy.ToVacancy();
            var newHash = VacancyEntity.FromVacancy(vacancyModel).ContentHash;

            if (vacancy.ContentHash != newHash)
            {
                _logger.LogInformation("Updating hash for {Title}: {OldHash} -> {NewHash}",
                    vacancy.Title, vacancy.ContentHash[..8] + "...", newHash[..8] + "...");
                vacancy.ContentHash = newHash;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Content hash recalculation completed");
    }
}