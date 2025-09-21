namespace DouVacancyAnalyzer.Models;

public class Vacancy
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string Experience { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public bool IsRemote { get; set; }
    public string Location { get; set; } = string.Empty;
    public List<string> Technologies { get; set; } = new();
    public string EnglishLevel { get; set; } = string.Empty;
}


