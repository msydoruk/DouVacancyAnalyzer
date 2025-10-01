using DouVacancyAnalyzer.Infrastructure.Data;
using DouVacancyAnalyzer.Core.Application.DTOs;
using DouVacancyAnalyzer.Core.Application.Interfaces;
using DouVacancyAnalyzer.Core.Domain.Entities;
using DouVacancyAnalyzer.Core.Domain.Enums;
using DouVacancyAnalyzer.Core.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DouVacancyAnalyzer.Core.Application.Services;

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
            .Where(v => v.IsNew && v.IsActive)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<VacancyEntity?> GetVacancyByUrlAsync(string url)
    {
        return await _context.Vacancies
            .FirstOrDefaultAsync(v => v.Url == url);
    }

    public async Task<List<VacancyEntity>> SaveVacanciesAsync(List<Vacancy> vacancies, bool markOthersAsInactive = false)
    {
        var savedVacancies = new List<VacancyEntity>();
        var newVacancyCount = 0;

        // Get all existing vacancy URLs for comparison
        var allExistingUrls = await _context.Vacancies
            .Select(v => v.Url)
            .ToListAsync();

        // Note: vacancies list contains ONLY NEW vacancies (existing ones were skipped during scraping)
        foreach (var vacancy in vacancies)
        {
            var entity = VacancyEntity.FromVacancy(vacancy);

            _logger.LogInformation("Processing NEW vacancy: {Title} | URL: {Url}",
                vacancy.Title, vacancy.Url);

            var existing = await GetVacancyByUrlAsync(vacancy.Url);

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
                // This should not happen since scraper skips existing vacancies
                _logger.LogWarning("⚠️ Vacancy already exists but was not skipped by scraper: {Title}", vacancy.Title);
                savedVacancies.Add(existing);
            }
        }

        _logger.LogInformation("Processed {TotalCount} new vacancies, saved {NewCount}",
            vacancies.Count, newVacancyCount);

        return savedVacancies;
    }

    public async Task UpdateVacancyAnalysisAsync(int vacancyId, VacancyAnalysisResult analysis)
    {
        var vacancy = await _context.Vacancies.FindAsync(vacancyId);
        if (vacancy == null) return;

        vacancy.VacancyCategory = analysis.VacancyCategory;
        vacancy.DetectedExperienceLevel = analysis.DetectedExperienceLevel;
        vacancy.DetectedYearsOfExperience = analysis.DetectedYearsOfExperience;
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

        // Mark as not new after first analysis
        if (vacancy.IsNew)
        {
            vacancy.IsNew = false;
            _logger.LogInformation("Marked vacancy as viewed after analysis: {Title}", vacancy.Title);
        }

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
            .Where(v => v.LastAnalyzedAt != null && v.IsActive)
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


    public async Task<VacancyCountHistory> CreateVacancyCountHistoryAsync(
        int totalVacancies, int activeVacancies, int newVacancies,
        int deactivatedVacancies, int matchingVacancies, decimal matchPercentage)
    {
        var history = new VacancyCountHistory
        {
            CheckDate = DateTime.UtcNow,
            TotalVacancies = totalVacancies,
            ActiveVacancies = activeVacancies,
            NewVacancies = newVacancies,
            DeactivatedVacancies = deactivatedVacancies,
            MatchingVacancies = matchingVacancies,
            MatchPercentage = matchPercentage
        };

        _context.VacancyCountHistory.Add(history);
        await _context.SaveChangesAsync();

        _logger.LogInformation("📊 Created vacancy count history: Total={Total}, Active={Active}, New={New}, Deactivated={Deactivated}, Matching={Matching}",
            totalVacancies, activeVacancies, newVacancies, deactivatedVacancies, matchingVacancies);

        return history;
    }

    public async Task<List<VacancyCountHistory>> GetVacancyCountHistoryAsync(int limit = AnalysisConstants.DefaultVacancyHistoryLimit)
    {
        return await _context.VacancyCountHistory
            .OrderByDescending(h => h.CheckDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<VacancyEntity>> GetActiveVacanciesAsync()
    {
        return await _context.Vacancies
            .Where(v => v.IsActive)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetActiveVacancyCountAsync()
    {
        return await _context.Vacancies.CountAsync(v => v.IsActive);
    }

    public async Task ResetAnalysisDataAsync()
    {
        var analysisCount = await _context.Vacancies
            .Where(v => v.LastAnalyzedAt != null)
            .ExecuteUpdateAsync(v => v
                .SetProperty(x => x.VacancyCategory, (VacancyCategory?)null)
                .SetProperty(x => x.DetectedExperienceLevel, (ExperienceLevel?)null)
                .SetProperty(x => x.DetectedYearsOfExperience, (string?)null)
                .SetProperty(x => x.DetectedEnglishLevel, (EnglishLevel?)null)
                .SetProperty(x => x.IsModernStack, (bool?)null)
                .SetProperty(x => x.IsMiddleLevel, (bool?)null)
                .SetProperty(x => x.HasAcceptableEnglish, (bool?)null)
                .SetProperty(x => x.HasNoTimeTracker, (bool?)null)
                .SetProperty(x => x.IsBackendSuitable, (bool?)null)
                .SetProperty(x => x.AnalysisReason, (string?)null)
                .SetProperty(x => x.MatchScore, (int?)null)
                .SetProperty(x => x.DetectedTechnologies, (string?)null)
                .SetProperty(x => x.LastAnalyzedAt, (DateTime?)null));

        _logger.LogInformation("🔄 Reset analysis data for {Count} vacancies", analysisCount);
    }

    public async Task<int> UpdateVacancyActivityStatusAsync(List<string> currentVacancyUrls)
    {
        // Mark vacancies that are no longer in the current list as inactive
        var deactivatedCount = await _context.Vacancies
            .Where(v => v.IsActive && !currentVacancyUrls.Contains(v.Url))
            .ExecuteUpdateAsync(v => v.SetProperty(x => x.IsActive, false));

        if (deactivatedCount > 0)
        {
            _logger.LogInformation("🔴 Deactivated {Count} vacancies that are no longer available", deactivatedCount);
        }

        return deactivatedCount;
    }
}