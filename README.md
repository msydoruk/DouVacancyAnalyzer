# 🚀 DOU Vacancy Analyzer

AI-powered tool for analyzing .NET job vacancies from DOU.ua with modern web interface and multi-language support.

## 🌐 Language Support

The application supports Ukrainian and English languages. Ukrainian is the default language, but you can switch languages easily:

- **Ukrainian**: `https://localhost:5001?culture=uk`
- **English**: `https://localhost:5001?culture=en`

## ✨ Features

- **🤖 AI Analysis**: Uses OpenAI GPT-4o-mini for intelligent vacancy analysis and categorization
- **📊 Real-time Progress**: Detailed progress tracking with logs for each vacancy processing
- **🌍 Multi-language**: Support for Ukrainian and English languages
- **📈 Interactive Dashboard**: Modern web interface with charts and statistics
- **🎯 Smart Filtering**: Automated matching based on criteria
- **⚡ Test Mode**: Quick analysis of few vacancies for testing before full analysis

## 📋 Requirements

- .NET 8.0 SDK
- OpenAI API key
- Internet connection
- Chrome browser (for web scraping)

## 🛠️ Setup

### 1. Clone and prepare

```bash
git clone <repository-url>
cd DouVacancyAnalyzer
```

### 2. Configuration setup

1. Copy the example configuration:
   ```bash
   copy DouVacancyAnalyzer\appsettings.example.json DouVacancyAnalyzer\appsettings.json
   ```

2. Edit `DouVacancyAnalyzer\appsettings.json` and insert your OpenAI API key:
   ```json
   {
     "OpenAiSettings": {
       "ApiKey": "sk-your-actual-openai-api-key-here",
       "Model": "gpt-4o-mini"
     }
   }
   ```

### 3. Getting OpenAI API Key

1. Sign up at [OpenAI Platform](https://platform.openai.com/)
2. Go to [API Keys](https://platform.openai.com/account/api-keys)
3. Click "Create new secret key"
4. Copy the key (it starts with `sk-`)

## 🚀 Running

### Option 1: Using configuration file

```bash
# Windows
run.bat

# PowerShell
.\run.ps1
```

### Option 2: With API key as parameter

```bash
# Windows
run.bat "sk-your-openai-api-key-here"

# PowerShell
.\run.ps1 -ApiKey "sk-your-openai-api-key-here"
```

### Option 3: Manual run

```bash
# Build
dotnet build DouVacancyAnalyzer.sln

# Run
cd DouVacancyAnalyzer
dotnet run
```

## 🧪 Test Mode

Before running a full analysis of all vacancies, you can test the application with a small sample:

1. **Quick Test**: Analyzes only the first 5 vacancies
   - Click "Test Analysis (5 vacancies)" button
   - Perfect for checking if OpenAI API is working
   - Takes ~1-2 minutes

2. **Full Analysis**: Analyzes all available vacancies
   - Click "Start Full Analysis" button
   - Can take 20-30 minutes depending on vacancy count
   - Recommended only after successful test

## ⚙️ Configuration Options

You can customize analysis criteria in `appsettings.json`:

```json
{
  "ScrapingSettings": {
    "BaseUrl": "https://jobs.dou.ua/vacancies/?category=.NET",
    "DelayBetweenRequests": 1000,
    "MaxPages": 10
  },
  "OpenAiSettings": {
    "ApiKey": "your-api-key",
    "Model": "gpt-4o-mini"
  }
}
```

## 🎯 Matching Criteria

The system evaluates vacancies based on:

1. **Modern Stack** (30 points): .NET Core, ASP.NET Core, Docker, Kubernetes, Azure, AWS
2. **Middle Level** (25 points): Suitable experience level for Middle developers
3. **Acceptable English** (25 points): Pre-Intermediate to Upper-Intermediate
4. **No Time Tracker** (20 points): Preference for positions without time tracking
5. **Backend Suitable** (Required): Backend, Fullstack, or DevOps positions

**Total possible score**: 100 points

## 📊 Analysis Results

The application provides:

- **Quick Overview**: Total vacancies, matching count, compliance percentage
- **Detailed Statistics**: Technology breakdown, experience levels
- **Interactive Charts**: Experience distribution, category breakdown
- **Top Technologies**: Most mentioned modern technologies
- **Matching Vacancies**: Top 20 best matches with scores and reasoning
- **Real-time Logs**: See each vacancy being processed with timestamps

## 🔧 Technology Stack

- **Backend**: ASP.NET Core 8.0
- **Frontend**: HTML5, CSS3, JavaScript, Bootstrap
- **Real-time**: SignalR for live updates
- **AI**: OpenAI GPT-4o-mini API
- **Scraping**: Selenium WebDriver + HtmlAgilityPack
- **Localization**: ASP.NET Core Resource files
- **Charts**: Chart.js

## 💡 Usage Tips

1. **Start with Test Mode**: Always run a test analysis first to verify setup
2. **Check API Credits**: Ensure you have sufficient OpenAI API credits
3. **Stable Connection**: Make sure you have a stable internet connection
4. **Monitor Progress**: Use the detailed progress logs to track analysis
5. **Language Switching**: Add `?culture=en` or `?culture=uk` to URL for language switching

## 🐛 Troubleshooting

### Error "OpenAI API key is not configured"
- Ensure you correctly copied the API key
- Check that the key starts with `sk-`
- Verify you have credits on your OpenAI account

### Error "Build failed"
- Ensure .NET 8.0 SDK is installed
- Run `dotnet --version` to verify

### Analysis stops or hangs
- Check your internet connection
- Verify OpenAI API key is valid and has credits
- Try running test mode first

### No vacancies found
- Check if DOU.ua is accessible
- Verify the scraping URL in configuration
- Try running at different times (avoid peak hours)

## 🔒 Security Notes

- Never commit your `appsettings.json` with real API keys to version control
- The `.gitignore` file is configured to exclude sensitive configuration files
- Use environment variables or Azure Key Vault for production deployments

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [DOU.ua](https://dou.ua) for providing job vacancy data
- [OpenAI](https://openai.com) for AI analysis capabilities
- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) for the web framework
- [Selenium](https://selenium.dev/) for web scraping capabilities

## ⚠️ Disclaimer

This tool is for educational and personal use only. Please respect DOU.ua's terms of service and rate limiting when scraping data. Be mindful of OpenAI API usage costs.

---

🤖 **Generated with [Claude Code](https://claude.ai/code)**

Co-Authored-By: Claude <noreply@anthropic.com>