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

builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAiSettings"));
builder.Services.Configure<ScrapingSettings>(builder.Configuration.GetSection("ScrapingSettings"));

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

builder.Services.AddSingleton<OpenAIClient>(provider =>
{
    var config = builder.Configuration.GetSection("OpenAiSettings").Get<OpenAiSettings>();
    if (string.IsNullOrEmpty(config?.ApiKey))
    {
        throw new InvalidOperationException("OpenAI API key is not configured. Please set it in appsettings.json or provide it as command line argument.");
    }
    return new OpenAIClient(config.ApiKey);
});

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
