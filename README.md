# DOU Vacancy Analyzer

Automated tool for analyzing .NET job vacancies from DOU.ua with AI-powered matching and response tracking.

## Features

### 🔍 **Vacancy Analysis**
- **AI-powered categorization** (Backend, Frontend, Fullstack, Desktop, QA)
- **Experience level detection** (Junior, Middle, Senior)
- **English level analysis** (from job descriptions)
- **Technology stack detection** (modern vs outdated)
- **Smart matching** against your criteria

### 📊 **Statistics & Analytics**
- **Match percentage** calculation
- **Technology trends** analysis
- **Experience level distribution** charts
- **Vacancy count history** with timeline graphs
- **Active vs inactive** vacancy tracking

### 🎯 **Response Management**
- **Track applications** with database storage
- **Filter by response status** (All, Responded, Not Responded)
- **Visual indicators** for responded vacancies
- **Real-time status updates** without caching

### 📈 **Historical Data**
- **Vacancy count trends** over time
- **Automatic deactivation** of removed vacancies
- **New vacancy detection** between runs
- **Timeline visualization** of job market changes

### ⚡ **Performance**
- **Parallel processing** for faster analysis
- **Rate limiting** to avoid API blocks
- **Progress tracking** with real-time updates
- **Optimized database** queries with indexes

## Quick Start

1. **Configure OpenAI API** in `appsettings.json`
2. **Run migrations**: `dotnet ef database update`
3. **Start application**: `dotnet run`
4. **Visit**: `http://localhost:5000`

## Usage

### Analysis Modes
- **Test Analysis** - Analyze 5 random vacancies
- **Full Analysis** - Complete job market scan
- **Fast Analysis** - Parallel processing mode

### Filtering Options
- View all vacancies or filter by response status
- Only active vacancies shown (removed ones hidden)
- Real-time filtering without page refresh

### Charts & Insights
- Experience level distribution (pie chart)
- Technology trends analysis
- Vacancy count history (line chart)
- Match rate statistics

## Technology Stack

- **Backend**: ASP.NET Core 8, Entity Framework, SQLite
- **AI**: OpenAI GPT-4 for intelligent analysis
- **Frontend**: JavaScript, Chart.js, Bootstrap 5
- **Real-time**: SignalR for progress updates

## Database Schema

- **Vacancies** - Job postings with analysis results
- **VacancyResponses** - Application tracking
- **VacancyCountHistory** - Historical statistics

## API Endpoints

- `POST /Home/StartAnalysis` - Run full analysis
- `GET /Home/GetVacancyResponses` - Get response status
- `POST /Home/ToggleVacancyResponse` - Update response status
- `GET /Home/GetVacancyCountHistory` - Historical data

## Configuration

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4"
  }
}
```