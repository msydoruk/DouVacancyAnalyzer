using DouVacancyAnalyzer.Hubs;
using DouVacancyAnalyzer.Models;
using DouVacancyAnalyzer.Services;
using DouVacancyAnalyzer.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var aiProvider = builder.Configuration.GetValue<string>("AiProvider") ?? "OpenAI";

builder.Services.Configure<ScrapingSettings>(builder.Configuration.GetSection("ScrapingSettings"));

// Configure AI provider
if (aiProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
{
    var anthropicConfig = builder.Configuration.GetSection("AnthropicSettings");
    var apiKey = anthropicConfig.GetValue<string>("ApiKey");
    var model = anthropicConfig.GetValue<string>("Model") ?? "claude-3-5-sonnet-20241022";

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("Anthropic API key is not configured. Please set it in appsettings.json.");
    }

    builder.Services.AddSingleton<IAiClient>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<AnthropicAiClient>>();
        return new AnthropicAiClient(apiKey, model, logger);
    });

    builder.Services.Configure<AnalysisPrompts>(anthropicConfig.GetSection("Prompts"));
}
else
{
    var openAiConfig = builder.Configuration.GetSection("OpenAiSettings");
    var apiKey = openAiConfig.GetValue<string>("ApiKey");
    var model = openAiConfig.GetValue<string>("Model") ?? "gpt-4o-mini";

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("OpenAI API key is not configured. Please set it in appsettings.json.");
    }

    var openAiClient = new OpenAIClient(apiKey);
    builder.Services.AddSingleton<IAiClient>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<OpenAiClient>>();
        return new OpenAiClient(openAiClient, model, logger);
    });

    builder.Services.Configure<AnalysisPrompts>(openAiConfig.GetSection("Prompts"));
}

// Add Entity Framework
builder.Services.AddDbContext<VacancyDbContext>(options =>
    options.UseSqlite("Data Source=vacancies.db"));

builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("uk"),
        new CultureInfo("en")
    };

    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("uk");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

builder.Services.AddHttpClient();

builder.Services.AddScoped<IVacancyScrapingService, VacancyScrapingService>();
builder.Services.AddScoped<IVacancyAnalysisService, VacancyAnalysisService>();
builder.Services.AddScoped<IVacancyStorageService, VacancyStorageService>();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = null; // Зберігає оригінальні назви властивостей
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

var supportedCultures = new[] { "uk", "en" };
var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
localizationOptions.ApplyCurrentCultureToResponseHeaders = true;

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRequestLocalization(localizationOptions);

app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<AnalysisHub>("/analysishub");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VacancyDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
