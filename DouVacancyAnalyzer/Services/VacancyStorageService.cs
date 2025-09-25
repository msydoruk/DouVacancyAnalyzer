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

    public async Task<VacancyEntity?> GetVacancyByHashAsync(string contentHash)
    {
        return await _context.Vacancies
            .FirstOrDefaultAsync(v => v.ContentHash == contentHash);
    }

    public async Task<List<VacancyEntity>> SaveVacanciesAsync(List<Vacancy> vacancies)
    {
        var savedVacancies = new List<VacancyEntity>();
        var newVacancyCount = 0;

        foreach (var vacancy in vacancies)
        {
            var entity = VacancyEntity.FromVacancy(vacancy);
            var existing = await GetVacancyByHashAsync(entity.ContentHash);

            _logger.LogInformation("Processing vacancy: {Title} | Hash: {Hash}",
                vacancy.Title, entity.ContentHash[..8] + "...");

            if (existing == null)
            {
                _context.Vacancies.Add(entity);
                savedVacancies.Add(entity);
                newVacancyCount++;
                _logger.LogInformation("✅ NEW vacancy added: {Title} at {Company} | Hash: {Hash}",
                    vacancy.Title, vacancy.Company, entity.ContentHash[..12] + "...");
            }
            else
            {
                // Update existing vacancy fields that might change
                existing.PublishedDate = vacancy.PublishedDate;
                existing.Salary = vacancy.Salary;
                existing.IsRemote = vacancy.IsRemote;
                existing.IsNew = false; // Mark as not new since it already existed
                savedVacancies.Add(existing);
                _logger.LogInformation("🔄 EXISTING vacancy updated: {Title} at {Company} | ID: {Id}",
                    vacancy.Title, vacancy.Company, existing.Id);
            }
        }

        if (newVacancyCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Saved {NewCount} new vacancies out of {TotalCount} processed",
                newVacancyCount, vacancies.Count);
        }

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