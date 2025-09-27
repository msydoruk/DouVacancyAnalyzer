const connection = new signalR.HubConnectionBuilder()
    .withUrl("/analysishub")
    .build();

const startButton = document.getElementById('startAnalysis');
const startTestButton = document.getElementById('startTestAnalysis');
const startOptimizedButton = document.getElementById('startOptimizedAnalysis');
const cancelButton = document.getElementById('cancelAnalysis');
const clearDatabaseButton = document.getElementById('clearDatabase');
const debugAnalysisButton = document.getElementById('debugAnalysis');
const progressSection = document.getElementById('progressSection');
const resultsSection = document.getElementById('resultsSection');
const progressBar = document.getElementById('progressBar');
const progressMessage = document.getElementById('progressMessage');
const progressLog = document.getElementById('progressLog');
const progressLogContent = document.getElementById('progressLogContent');

let experienceChart = null;
let categoryChart = null;
let currentLogEntry = null;
let isTestMode = false;
let analysisController = null;

// Response status tracking
const RESPONSE_STATUS_KEY = 'vacancy_response_status';
let currentFilter = 'all';
let currentVacancies = [];

// Функція для нормалізації даних з урахуванням camelCase від SignalR
function normalizeDataKeys(data) {
    console.log('🔄 normalizeDataKeys input:', data);

    if (!data) return null;

    let report = data.Report || data.report;
    if (report) {
        // Normalize the report structure including matches
        const matches = report.Matches || report.matches || [];
        console.log('📋 Original matches:', matches);

        // Normalize each match structure
        const normalizedMatches = matches.map(match => {
            const vacancy = match.Vacancy || match.vacancy;
            const analysis = match.Analysis || match.analysis;

            // Normalize Vacancy fields to support both cases
            const normalizedVacancy = vacancy ? {
                Title: vacancy.Title || vacancy.title,
                Company: vacancy.Company || vacancy.company,
                Description: vacancy.Description || vacancy.description,
                Url: vacancy.Url || vacancy.url,
                PublishedDate: vacancy.PublishedDate || vacancy.publishedDate,
                Experience: vacancy.Experience || vacancy.experience,
                Salary: vacancy.Salary || vacancy.salary,
                IsRemote: vacancy.IsRemote !== undefined ? vacancy.IsRemote : vacancy.isRemote,
                Location: vacancy.Location || vacancy.location,
                Technologies: vacancy.Technologies || vacancy.technologies,
                EnglishLevel: vacancy.EnglishLevel || vacancy.englishLevel
            } : null;

            // Normalize Analysis fields to support both cases
            const normalizedAnalysis = analysis ? {
                VacancyCategory: analysis.VacancyCategory || analysis.vacancyCategory,
                DetectedExperienceLevel: analysis.DetectedExperienceLevel || analysis.detectedExperienceLevel,
                DetectedEnglishLevel: analysis.DetectedEnglishLevel || analysis.detectedEnglishLevel,
                IsModernStack: analysis.IsModernStack !== undefined ? analysis.IsModernStack : analysis.isModernStack,
                IsMiddleLevel: analysis.IsMiddleLevel !== undefined ? analysis.IsMiddleLevel : analysis.isMiddleLevel,
                HasAcceptableEnglish: analysis.HasAcceptableEnglish !== undefined ? analysis.HasAcceptableEnglish : analysis.hasAcceptableEnglish,
                HasNoTimeTracker: analysis.HasNoTimeTracker !== undefined ? analysis.HasNoTimeTracker : analysis.hasNoTimeTracker,
                IsBackendSuitable: analysis.IsBackendSuitable !== undefined ? analysis.IsBackendSuitable : analysis.isBackendSuitable,
                AnalysisReason: analysis.AnalysisReason || analysis.analysisReason,
                MatchScore: analysis.MatchScore !== undefined ? analysis.MatchScore : analysis.matchScore,
                DetectedTechnologies: analysis.DetectedTechnologies || analysis.detectedTechnologies
            } : null;

            return {
                Vacancy: normalizedVacancy,
                Analysis: normalizedAnalysis
            };
        });

        console.log('📋 Normalized matches:', normalizedMatches);

        report = {
            TotalVacancies: report.TotalVacancies || report.totalVacancies,
            MatchingVacancies: report.MatchingVacancies || report.matchingVacancies,
            MatchPercentage: report.MatchPercentage || report.matchPercentage,
            Matches: normalizedMatches
        };
    }

    const result = {
        Report: report,
        TechStats: data.TechStats || data.techStats,
        AiStats: data.AiStats || data.aiStats
    };

    console.log('🔄 normalizeDataKeys output:', result);
    return result;
}
connection.start().then(function () {
    console.log('SignalR connection established');
}).catch(function (err) {
    console.error('SignalR connection error: ', err.toString());
});

startButton.addEventListener('click', function() {
    isTestMode = false;
    startAnalysis();
});

startTestButton.addEventListener('click', function() {
    isTestMode = true;
    startTestAnalysis();
});

startOptimizedButton.addEventListener('click', function() {
    isTestMode = false;
    startOptimizedAnalysis();
});

cancelButton.addEventListener('click', function() {
    if (analysisController) {
        analysisController.abort();
        cancelAnalysis();
    }
});

clearDatabaseButton.addEventListener('click', function() {
    if (confirm('Are you sure you want to clear the database? This will remove all stored vacancies and analysis results.')) {
        clearDatabase();
    }
});

debugAnalysisButton.addEventListener('click', function() {
    debugAnalysis();
});

connection.on("AnalysisStarted", function () {
    // Блокуємо тільки ту кнопку, яка була натиснута
    if (isTestMode) {
        startTestButton.disabled = true;
        startTestButton.innerHTML = `<i class="fas fa-spinner fa-spin me-2"></i>${window.localization.analysisInProgress}`;
    } else {
        startButton.disabled = true;
        startButton.innerHTML = `<i class="fas fa-spinner fa-spin me-2"></i>${window.localization.analysisInProgress}`;
    }

    cancelButton.style.display = 'inline-block';
    progressSection.style.display = 'block';
    resultsSection.style.display = 'none';
    progressLog.style.display = 'block';
    progressLogContent.innerHTML = '';
    currentLogEntry = null;
    updateProgress(0, window.localization.preparingAnalysis);
    addLogEntry(window.localization.analysisStarted, 'info');
});

connection.on("ProgressUpdate", function (message, progress) {
    updateProgress(progress, message);

    if (message.includes('🤖 Аналізую')) {
        if (currentLogEntry) {
            currentLogEntry.classList.remove('current');
            currentLogEntry.classList.add('completed');
        }

        currentLogEntry = addLogEntry(message, 'current');
    } else {
        addLogEntry(message, 'info');
    }
});

connection.on("AnalysisCompleted", function (data) {
    console.log('=== ANALYSIS COMPLETED ===');
    console.log('Raw data received:', data);
    console.log('Data type:', typeof data);
    console.log('Data.Report:', data ? data.Report : 'undefined');
    console.log('Data.TechStats:', data ? data.TechStats : 'undefined');
    console.log('Data.AiStats:', data ? data.AiStats : 'undefined');

    updateProgress(100, `✅ ${window.localization.analysisCompleted}!`);

    if (currentLogEntry) {
        currentLogEntry.classList.remove('current');
        currentLogEntry.classList.add('completed');
    }

    addLogEntry(window.localization.analysisCompleted, 'completed');

    setTimeout(async () => {
        progressSection.style.display = 'none';
        resultsSection.style.display = 'block';

        // Детальна перевірка структури даних
        if (!data) {
            console.error('❌ Data is null or undefined');
            alert('Не отримано жодних даних');
            return;
        }

        console.log('Data keys:', Object.keys(data));

        // Нормалізуємо ключі даних
        const normalizedData = normalizeDataKeys(data);

        console.log('Normalized data:', normalizedData);

        if (!normalizedData.Report) {
            console.error('❌ Report is missing');
            console.log('Available properties:', Object.keys(data));
        }

        if (!normalizedData.TechStats) {
            console.error('❌ TechStats is missing');
            console.log('Available properties:', Object.keys(data));
        }

        if (normalizedData.Report && normalizedData.TechStats) {
            console.log('✅ Valid data structure - proceeding with display');
            console.log('Report keys:', Object.keys(normalizedData.Report));
            console.log('TechStats keys:', Object.keys(normalizedData.TechStats));
            await displayResults(normalizedData);
        } else {
            console.error('❌ Invalid data structure received');
            console.error('original data:', data);
            console.error('normalized data:', normalizedData);
            alert('Отримано некоректні дані результатів. Перевірте консоль для деталей.');
        }

        resetButtons();
    }, 1000);
});

connection.on("AnalysisError", function (error) {
    updateProgress(0, `❌ ${window.localization.error.replace('{0}', error)}`);
    progressBar.classList.add('bg-danger');

    if (currentLogEntry) {
        currentLogEntry.classList.remove('current');
        currentLogEntry.classList.add('error');
    }

    addLogEntry(`❌ ${window.localization.error.replace('{0}', error)}`, 'error');
    resetButtons();
});

function startAnalysis() {
    analysisController = new AbortController();

    fetch('/Home/StartAnalysis', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        signal: analysisController.signal
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            alert('Помилка запуску аналізу: ' + data.error);
            resetButtons();
        }
    })
    .catch(error => {
        if (error.name === 'AbortError') {
            console.log('Analysis cancelled by user');
        } else {
            console.error('Error:', error);
            alert('Помилка запуску аналізу');
        }
        resetButtons();
    });
}

function startTestAnalysis() {
    analysisController = new AbortController();

    fetch('/Home/StartTestAnalysis', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        signal: analysisController.signal
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            alert('Помилка запуску тестового аналізу: ' + data.error);
            resetButtons();
        }
    })
    .catch(error => {
        if (error.name === 'AbortError') {
            console.log('Test analysis cancelled by user');
        } else {
            console.error('Error:', error);
            alert('Помилка запуску тестового аналізу');
        }
        resetButtons();
    });
}

function startOptimizedAnalysis() {
    analysisController = new AbortController();

    fetch('/Home/StartOptimizedAnalysis', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        signal: analysisController.signal
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            alert('Помилка запуску оптимізованого аналізу: ' + data.error);
            resetButtons();
        }
    })
    .catch(error => {
        if (error.name === 'AbortError') {
            console.log('Optimized analysis cancelled by user');
        } else {
            console.error('Error:', error);
            alert('Помилка запуску оптимізованого аналізу');
        }
        resetButtons();
    });
}

function updateProgress(progress, message) {
    progressBar.style.width = progress + '%';
    progressBar.setAttribute('aria-valuenow', progress);
    progressBar.textContent = progress + '%';
    progressMessage.textContent = message;
}

function resetButtons() {
    startButton.disabled = false;
    startTestButton.disabled = false;
    startButton.innerHTML = `<i class="fas fa-play me-2"></i>${window.localization.startAnalysis}`;
    startTestButton.innerHTML = `<i class="fas fa-flask me-2"></i>${window.localization.startTestAnalysis}`;
    progressBar.classList.remove('bg-danger');
    cancelButton.style.display = 'none';
    isTestMode = false;
    analysisController = null; // Скидаємо режим
}

function addLogEntry(message, type = 'info') {
    const timestamp = new Date().toLocaleTimeString('uk-UA', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });

    const entry = document.createElement('div');
    entry.className = `progress-log-entry ${type}`;

    entry.innerHTML = `
        <span class="timestamp">${timestamp}</span>
        <span class="vacancy-info">${message}</span>
    `;

    progressLogContent.appendChild(entry);

    progressLogContent.scrollTop = progressLogContent.scrollHeight;

    return entry;
}

function cancelAnalysis() {
    updateProgress(0, 'Analysis cancelled');
    progressBar.classList.add('bg-warning');

    if (currentLogEntry) {
        currentLogEntry.classList.remove('current');
        currentLogEntry.classList.add('error');
    }

    addLogEntry('Analysis cancelled by user', 'error');
    resetButtons();

    setTimeout(() => {
        progressSection.style.display = 'none';
    }, 2000);
}

async function displayResults(data) {
    console.log('🎯 displayResults called with data:', data);

    const { Report, TechStats, AiStats } = data;

    console.log('📊 Report:', Report);
    console.log('🔧 TechStats:', TechStats);
    console.log('🤖 AiStats:', AiStats);

    if (!Report) {
        console.error('❌ Report is missing');
        return;
    }

    if (!TechStats) {
        console.error('❌ TechStats is missing');
        return;
    }

    try {
        // Show the results section
        const resultsSection = document.getElementById('resultsSection');
        if (resultsSection) {
            resultsSection.style.display = 'block';
            console.log('✅ Results section shown');
        } else {
            console.error('❌ Results section element not found');
            return;
        }

        displayQuickSummary(Report, AiStats);
        displayAnalysisStats(Report);
        displayTechStats(TechStats);
        displayModernTech(AiStats, Report);
        await displayVacancies(Report.Matches || []);
        displayNewVacancies();
        displayCharts(TechStats, AiStats || {});
        displayVacancyCountHistory();
        console.log('All display functions completed successfully');
    } catch (error) {
        console.error('Error in displayResults:', error);
    }
}

function displayQuickSummary(report, aiStats) {
    console.log('📊 displayQuickSummary called with report:', report, 'aiStats:', aiStats);

    if (!report) {
        console.error('❌ Report is null or undefined in displayQuickSummary');
        return;
    }

    if (!aiStats) {
        console.error('❌ AiStats is null or undefined in displayQuickSummary');
        return;
    }

    const container = document.getElementById('quickSummary');
    const matchPercentage = (report && typeof report.MatchPercentage === 'number') ? report.MatchPercentage : 0;

    // Support both PascalCase and camelCase for aiStats
    const aiTotal = aiStats.Total || aiStats.total || 0;
    const aiWithModernTech = aiStats.WithModernTech || aiStats.withModernTech || 0;
    const modernPercentage = aiTotal > 0 ? ((aiWithModernTech / aiTotal) * 100).toFixed(1) : 0;

    console.log('📈 Quick summary values:', {
        totalVacancies: report.TotalVacancies,
        matchingVacancies: report.MatchingVacancies,
        matchPercentage,
        modernPercentage,
        aiTotal,
        aiWithModernTech
    });

    container.innerHTML = `
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-primary">${report.TotalVacancies || 0}</div>
                <div class="summary-label">${window.localization.totalVacancies}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-success">${report.MatchingVacancies || 0}</div>
                <div class="summary-label">${window.localization.matching}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-warning">${(matchPercentage || 0).toFixed(1)}%</div>
                <div class="summary-label">${window.localization.compliance}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-info">${modernPercentage || 0}%</div>
                <div class="summary-label">${window.localization.modernTech}</div>
            </div>
        </div>
    `;
}

function displayAnalysisStats(report) {
    console.log('📊 displayAnalysisStats called with report:', report);

    const container = document.getElementById('analysisStats');
    const matches = report.Matches || [];
    const modernStackCount = matches.filter(m => m.Analysis && m.Analysis.IsModernStack).length;
    const middleLevelCount = matches.filter(m => m.Analysis && m.Analysis.IsMiddleLevel).length;
    const acceptableEnglishCount = matches.filter(m => m.Analysis && m.Analysis.HasAcceptableEnglish === true).length;
    const noTimeTrackerCount = matches.filter(m => m.Analysis && m.Analysis.HasNoTimeTracker !== false).length;

    console.log('📈 Analysis Stats Counts:');
    console.log('- Total matches:', matches.length);
    console.log('- Modern Stack (AI):', modernStackCount);
    console.log('- Middle Level:', middleLevelCount);
    console.log('- Acceptable English:', acceptableEnglishCount);
    console.log('- No Time Tracker:', noTimeTrackerCount);

    // Debug individual matches to see their structure
    console.log('🔍 Sample matches for debugging:');
    matches.slice(0, 3).forEach((match, index) => {
        console.log(`Match ${index + 1}:`);
        console.log(`  Title: ${match.Vacancy?.Title || match.vacancy?.title}`);
        console.log(`  Company: ${match.Vacancy?.Company || match.vacancy?.company}`);
        console.log(`  IsModernStack: ${match.Analysis?.IsModernStack}`);
        console.log(`  IsMiddleLevel: ${match.Analysis?.IsMiddleLevel}`);
        console.log(`  HasAcceptableEnglish: ${match.Analysis?.HasAcceptableEnglish}`);
        console.log(`  HasNoTimeTracker: ${match.Analysis?.HasNoTimeTracker}`);
        console.log(`  MatchScore: ${match.Analysis?.MatchScore}`);
        console.log(`  Full Analysis:`, match.Analysis);
        console.log(`  Full Vacancy:`, match.Vacancy);
        console.log(`  Full Match:`, match);
    });

    container.innerHTML = `
        <div class="row g-3">
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-list-ul fa-2x text-primary"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${report.TotalVacancies || 0}</h4>
                        <small class="text-muted">Total Vacancies</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-check-circle fa-2x text-success"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${report.MatchingVacancies || 0}</h4>
                        <small class="text-muted">Matching Vacancies</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-chart-line fa-2x text-warning"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${(report.MatchPercentage || 0).toFixed(1)}%</h4>
                        <small class="text-muted">Match Rate</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-cogs fa-2x text-info"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${modernStackCount}</h4>
                        <small class="text-muted">Modern Stack (AI)</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-user-tie fa-2x text-secondary"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${middleLevelCount}</h4>
                        <small class="text-muted">Middle Level</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-globe fa-2x text-primary"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${acceptableEnglishCount}</h4>
                        <small class="text-muted">Good English</small>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function displayTechStats(techStats) {
    console.log('🔧 displayTechStats called with techStats:', techStats);

    const container = document.getElementById('techStats');

    // Support both PascalCase and camelCase for compatibility
    const withModernTech = techStats.WithModernTech || techStats.withModernTech || 0;
    const withOutdatedTech = techStats.WithOutdatedTech || techStats.withOutdatedTech || 0;
    const withDesktopApps = techStats.WithDesktopApps || techStats.withDesktopApps || 0;
    const withFrontend = techStats.WithFrontend || techStats.withFrontend || 0;
    const withTimeTracker = techStats.WithTimeTracker || techStats.withTimeTracker || 0;

    console.log('📊 TechStats values:', {
        withModernTech, withOutdatedTech, withDesktopApps, withFrontend, withTimeTracker
    });

    container.innerHTML = `
        <div class="row g-3">
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-rocket fa-2x text-success"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${withModernTech}</h4>
                        <small class="text-muted">Modern Tech (Keyword)</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-trash-alt fa-2x text-danger"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${withOutdatedTech}</h4>
                        <small class="text-muted">Outdated Tech</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-desktop fa-2x text-info"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${withDesktopApps}</h4>
                        <small class="text-muted">Desktop Apps</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-palette fa-2x text-warning"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${withFrontend}</h4>
                        <small class="text-muted">Frontend Positions</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-stopwatch fa-2x text-secondary"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${withTimeTracker}</h4>
                        <small class="text-muted">With Time Tracker</small>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function displayModernTech(aiStats, report) {
    console.log('🔥 displayModernTech called with aiStats:', aiStats);
    console.log('📊 report.Matches count:', (report.Matches || []).length);

    const container = document.getElementById('modernTech');
    // Use actual matches with modern stack instead of aiStats.ModernVacancies
    const matches = report.Matches || [];

    console.log('🔍 Analyzing all matches for modern stack:');
    matches.forEach((match, index) => {
        console.log(`  Match ${index + 1}:`, {
            title: match.Vacancy?.Title,
            company: match.Vacancy?.Company,
            isModernStack: match.Analysis?.IsModernStack,
            matchScore: match.Analysis?.MatchScore,
            detectedTechnologies: match.Analysis?.DetectedTechnologies,
            fullAnalysis: match.Analysis
        });
    });

    const modernVacancies = matches.filter(m => {
        const isModern = m.Analysis && m.Analysis.IsModernStack;
        console.log(`  ${m.Vacancy?.Title}: IsModernStack = ${isModern}`);
        return isModern;
    });

    console.log('✅ Modern stack vacancies found:', modernVacancies.length);

    if (modernVacancies.length === 0) {
        container.innerHTML = '<div class="text-center text-muted py-4"><i class="fas fa-info-circle fa-2x mb-2"></i><p>No modern stack vacancies found in matches</p></div>';
        return;
    }

    container.innerHTML = `
        <div class="table-responsive">
            <table class="table table-hover">
                <thead class="thead-light">
                    <tr>
                        <th>Job Title</th>
                        <th>Company</th>
                        <th>Location</th>
                        <th>Technologies</th>
                        <th>Score</th>
                        <th>Link</th>
                    </tr>
                </thead>
                <tbody>
                    ${modernVacancies.slice(0, 10).map(match => `
                        <tr>
                            <td>
                                <strong>${match.Vacancy.Title}</strong>
                                <br><small class="text-muted">${match.Vacancy.Experience || 'Not specified'}</small>
                            </td>
                            <td>${match.Vacancy.Company}</td>
                            <td>${match.Vacancy.Location}</td>
                            <td>
                                ${(match.Analysis.DetectedTechnologies || []).slice(0, 3).map(tech =>
                                    `<span class="badge bg-primary me-1">${tech}</span>`
                                ).join('')}
                                ${(match.Analysis.DetectedTechnologies || []).length > 3 ?
                                    `<span class="text-muted">+${(match.Analysis.DetectedTechnologies || []).length - 3} more</span>` : ''
                                }
                            </td>
                            <td>
                                <span class="badge bg-success">${Math.round(match.Analysis.MatchScore || 0)}%</span>
                            </td>
                            <td>
                                <a href="${match.Vacancy.Link}" target="_blank" class="btn btn-sm btn-outline-primary">
                                    <i class="fas fa-external-link-alt"></i>
                                </a>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
        ${modernVacancies.length > 10 ? `
            <div class="text-center mt-3">
                <small class="text-muted">Showing top 10 of ${modernVacancies.length} modern vacancies</small>
            </div>
        ` : ''}
    `;
}

async function displayVacancies(matches) {
    console.log('displayVacancies called with matches:', matches);

    // Store matches globally for filtering
    currentVacancies = matches || [];

    const tbody = document.querySelector('#vacancyTable tbody');
    const matchesList = matches || [];

    if (matchesList.length === 0) {
        tbody.innerHTML = '<tr><td colspan="9" class="text-center text-muted py-4">No matching vacancies found</td></tr>';
        return;
    }

    // Use the new table update function (no cache loading needed)
    await updateVacancyTable(matchesList);
}

function displayCharts(techStats, aiStats) {
    console.log('📊 displayCharts called with:', { techStats, aiStats });

    const expCtx = document.getElementById('experienceChart').getContext('2d');
    if (experienceChart) {
        experienceChart.destroy();
    }

    // Support both PascalCase and camelCase for aiStats
    const juniorLevel = aiStats.JuniorLevel || aiStats.juniorLevel || 0;
    const middleLevel = aiStats.MiddleLevel || aiStats.middleLevel || 0;
    const seniorLevel = aiStats.SeniorLevel || aiStats.seniorLevel || 0;
    const unspecifiedLevel = aiStats.UnspecifiedLevel || aiStats.unspecifiedLevel || 0;

    console.log('📈 Experience levels:', { juniorLevel, middleLevel, seniorLevel, unspecifiedLevel });

    experienceChart = new Chart(expCtx, {
        type: 'doughnut',
        data: {
            labels: ['Junior', 'Middle', 'Senior', 'Не вказано'],
            datasets: [{
                data: [juniorLevel, middleLevel, seniorLevel, unspecifiedLevel],
                backgroundColor: [
                    '#28a745',
                    '#ffc107',
                    '#dc3545',
                    '#6c757d'
                ]
            }]
        },
        options: {
            responsive: true,
            plugins: {
                title: {
                    display: true,
                    text: 'Розподіл за рівнем досвіду'
                }
            }
        }
    });

    // Handle category chart with support for both cases
    const vacancyCategories = aiStats.VacancyCategories || aiStats.vacancyCategories || {};
    console.log('📋 Vacancy categories:', vacancyCategories);

    if (Object.keys(vacancyCategories).length > 0) {
        const catCtx = document.getElementById('categoryChart').getContext('2d');
        if (categoryChart) {
            categoryChart.destroy();
        }

        categoryChart = new Chart(catCtx, {
            type: 'bar',
            data: {
                labels: Object.keys(vacancyCategories),
                datasets: [{
                    label: 'Кількість вакансій',
                    data: Object.values(vacancyCategories),
                    backgroundColor: [
                        '#007bff',
                        '#28a745',
                        '#ffc107',
                        '#dc3545',
                        '#17a2b8',
                        '#6f42c1'
                    ]
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    title: {
                        display: true,
                        text: 'AI категоризація вакансій'
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    } else {
        console.log('❌ No vacancy categories data available for chart');
    }
}

async function displayVacancyCountHistory() {
    try {
        console.log('📊 Loading vacancy count history...');
        const response = await fetch('/Home/GetVacancyCountHistory');
        const data = await response.json();

        if (data.success && data.history && data.history.length > 0) {
            console.log('📈 Displaying vacancy count history chart with', data.history.length, 'entries');
            createVacancyHistoryChart(data.history);
        } else {
            console.log('📊 No vacancy count history available');
        }
    } catch (error) {
        console.error('❌ Error loading vacancy count history:', error);
    }
}

function createVacancyHistoryChart(historyData) {
    // Show the history section
    let historySection = document.getElementById('vacancyHistorySection');
    if (!historySection) {
        // Create the section if it doesn't exist
        const resultsSection = document.getElementById('resultsSection');
        historySection = document.createElement('div');
        historySection.id = 'vacancyHistorySection';
        historySection.className = 'col-md-12 mb-4';
        historySection.innerHTML = `
            <div class="card border-0 shadow">
                <div class="card-header bg-info text-white">
                    <h5 class="mb-0">
                        <i class="fas fa-chart-line me-2"></i>
                        Vacancy Count History
                    </h5>
                </div>
                <div class="card-body p-4">
                    <canvas id="historyChart"></canvas>
                </div>
            </div>
        `;
        resultsSection.appendChild(historySection);
    }

    historySection.style.display = 'block';

    // Sort data by date
    const sortedData = [...historyData].sort((a, b) => new Date(a.CheckDate) - new Date(b.CheckDate));

    const ctx = document.getElementById('historyChart').getContext('2d');

    // Destroy existing chart if it exists
    if (window.historyChart) {
        window.historyChart.destroy();
    }

    window.historyChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: sortedData.map(item => new Date(item.CheckDate).toLocaleDateString()),
            datasets: [
                {
                    label: 'Total Vacancies',
                    data: sortedData.map(item => item.TotalVacancies),
                    borderColor: '#007bff',
                    backgroundColor: 'rgba(0, 123, 255, 0.1)',
                    tension: 0.1
                },
                {
                    label: 'Active Vacancies',
                    data: sortedData.map(item => item.ActiveVacancies),
                    borderColor: '#28a745',
                    backgroundColor: 'rgba(40, 167, 69, 0.1)',
                    tension: 0.1
                },
                {
                    label: 'New Vacancies',
                    data: sortedData.map(item => item.NewVacancies),
                    borderColor: '#ffc107',
                    backgroundColor: 'rgba(255, 193, 7, 0.1)',
                    tension: 0.1
                },
                {
                    label: 'Matching Vacancies',
                    data: sortedData.map(item => item.MatchingVacancies),
                    borderColor: '#dc3545',
                    backgroundColor: 'rgba(220, 53, 69, 0.1)',
                    tension: 0.1
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                title: {
                    display: true,
                    text: 'Vacancy Count Over Time'
                },
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Number of Vacancies'
                    }
                },
                x: {
                    title: {
                        display: true,
                        text: 'Date'
                    }
                }
            }
        }
    });
}

// Database management functionality

// Load stored analysis on page load
document.addEventListener('DOMContentLoaded', async function() {
    await loadStoredAnalysisIfAvailable();
});




async function loadStoredAnalysisIfAvailable() {
    console.log('🔍 Checking for stored analysis on page load...');

    // Check if there are any analyzed vacancies in the database
    try {
        const response = await fetch('/Home/GetDatabaseStats');
        const data = await response.json();
        console.log('📊 Database stats response:', data);

        if (data.success && data.totalVacancies > 0) {
            console.log(`✅ Found ${data.totalVacancies} stored vacancies, loading analysis automatically...`);
            await loadStoredAnalysis(false); // false = don't show loading UI
        } else {
            console.log('❌ No stored vacancies found, totalVacancies:', data.totalVacancies);

            // Try to load anyway in case there's some data
            console.log('🔄 Attempting to load stored analysis anyway...');
            await loadStoredAnalysis(false);
        }
    } catch (error) {
        console.error('❌ Error checking for stored analysis:', error);

        // Try to load anyway
        console.log('🔄 Attempting to load stored analysis despite error...');
        await loadStoredAnalysis(false);
    }
}

async function loadStoredAnalysis(showLoadingUI = true) {
    console.log('📥 Loading stored analysis... showLoadingUI:', showLoadingUI);

    if (showLoadingUI) {
        startButton.disabled = true;
        startTestButton.disabled = true;
    }

    try {
        const response = await fetch('/Home/GetStoredAnalysis');
        console.log('📡 GetStoredAnalysis response status:', response.status);
        const data = await response.json();
        console.log('📋 loadStoredAnalysis API response:', data);

        if (data.success && data.data) {
            console.log('✅ Analysis data found, displaying results...');
            console.log('📊 Report data:', data.data.Report);
            console.log('🔧 TechStats data:', data.data.TechStats);
            console.log('🤖 AiStats data:', data.data.AiStats);

            // Normalize the data structure for compatibility
            const normalizedData = normalizeDataKeys(data.data);
            console.log('🔄 Normalized data for display:', normalizedData);

            await displayResults(normalizedData);

            // Show notification about new vacancies if any
            if (data.data.HasNewVacancies) {
                const newCount = data.data.DatabaseStats.newCount;
                if (showLoadingUI) { // Only show notification if user manually requested
                    showNotification(`Found ${newCount} new vacancies since last analysis!`, 'info');
                }
            }

            // Show quiet notification on auto-load
            if (!showLoadingUI && data.data.Report && data.data.Report.TotalVacancies > 0) {
                console.log(`✅ Auto-loaded analysis with ${data.data.Report.TotalVacancies} total vacancies`);
            }
        } else {
            console.error('❌ API Error:', data.error || 'Unknown error');
            console.log('🔍 Full response data:', data);
            if (showLoadingUI) {
                showNotification('Error loading stored analysis: ' + (data.error || 'Unknown error'), 'error');
            }
        }
    } catch (error) {
        console.error('Error loading stored analysis:', error);
        if (showLoadingUI) {
            showNotification('Error loading stored analysis', 'error');
        }
    } finally {
        if (showLoadingUI) {
            startButton.disabled = false;
            startTestButton.disabled = false;
        }
    }
}

function clearDatabase() {
    clearDatabaseButton.disabled = true;
    clearDatabaseButton.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Clearing...';

    fetch('/Home/ClearDatabase', {
        method: 'POST'
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showNotification('Database cleared successfully', 'success');
            resultsSection.style.display = 'none';
        } else {
            showNotification('Error: ' + data.error, 'error');
        }
    })
    .catch(error => {
        console.error('Error clearing database:', error);
        showNotification('Error clearing database', 'error');
    })
    .finally(() => {
        clearDatabaseButton.disabled = false;
        clearDatabaseButton.innerHTML = '<i class="fas fa-trash me-2"></i>Clear Database';
    });
}

function showNotification(message, type) {
    const alertClass = type === 'error' ? 'alert-danger' :
                      type === 'success' ? 'alert-success' : 'alert-info';

    const notification = document.createElement('div');
    notification.className = `alert ${alertClass} alert-dismissible fade show position-fixed`;
    notification.style.top = '20px';
    notification.style.right = '20px';
    notification.style.zIndex = '9999';
    notification.style.maxWidth = '400px';

    notification.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;

    document.body.appendChild(notification);

    // Auto-remove after 5 seconds
    setTimeout(() => {
        if (notification.parentNode) {
            notification.parentNode.removeChild(notification);
        }
    }, 5000);
}

function displayNewVacancies() {
    fetch('/Home/GetNewVacancies')
        .then(response => response.json())
        .then(data => {
            if (data.success && data.data && data.data.length > 0) {
                const section = document.getElementById('newVacanciesSection');
                const tbody = document.querySelector('#newVacancyTable tbody');

                section.style.display = 'block';

                tbody.innerHTML = data.data.map((vacancy, index) => {
                    // Support both PascalCase and camelCase
                    const title = vacancy.Title || vacancy.title || 'Not specified';
                    const company = vacancy.Company || vacancy.company || 'Not specified';
                    const location = vacancy.Location || vacancy.location || 'Not specified';
                    const experience = vacancy.Experience || vacancy.experience || 'Not specified';
                    const englishLevel = vacancy.EnglishLevel || vacancy.englishLevel || 'Not specified';
                    const url = vacancy.Url || vacancy.url || '#';
                    const isAnalyzed = vacancy.IsAnalyzed !== undefined ? vacancy.IsAnalyzed : vacancy.isAnalyzed;

                    const statusBadge = isAnalyzed
                        ? '<span class="badge bg-success">Analyzed</span>'
                        : '<span class="badge bg-warning">Not analyzed</span>';

                    return `
                        <tr class="align-middle">
                            <td><span class="badge bg-primary">${index + 1}</span></td>
                            <td class="fw-bold">${title}</td>
                            <td>${company}</td>
                            <td><i class="fas fa-map-marker-alt text-muted me-1"></i>${location}</td>
                            <td><span class="badge bg-info">${experience}</span></td>
                            <td><span class="badge bg-success">${englishLevel}</span></td>
                            <td>${statusBadge}</td>
                            <td>
                                <a href="${url}" target="_blank" class="btn btn-outline-primary btn-sm">
                                    <i class="fas fa-external-link-alt me-1"></i>View
                                </a>
                            </td>
                        </tr>
                    `;
                }).join('');

                console.log(`Displayed ${data.data.length} new vacancies`);
            } else {
                console.log('No new vacancies found');
                const section = document.getElementById('newVacanciesSection');
                section.style.display = 'none';
            }
        })
        .catch(error => {
            console.error('Error loading new vacancies:', error);
        });
}

function debugAnalysis() {
    console.log('🐛 Starting debug analysis...');

    fetch('/Home/DebugStoredAnalysis')
        .then(response => response.json())
        .then(data => {
            console.log('🐛 Debug response:', data);

            if (data.success) {
                console.log('📊 Debug info:', data.debug);
                alert(`Debug Info:

Total Vacancies in DB: ${data.debug.TotalVacanciesInDb}
New Vacancies Count: ${data.debug.NewVacanciesCount}
Vacancies with Analysis: ${data.debug.VacanciesWithAnalysisCount}
Analyzed Vacancies: ${data.debug.AnalyzedVacanciesCount}

Sample vacancy analysis (check console for details)`);

                console.log('🔍 Sample vacancy analysis:', data.debug.SampleVacancyAnalysis);
            } else {
                console.error('❌ Debug failed:', data.error);
                alert('Debug failed: ' + data.error);
            }
        })
        .catch(error => {
            console.error('❌ Debug error:', error);
            alert('Debug error: ' + error.message);
        });
}

// ===== RESPONSE STATUS MANAGEMENT =====

// Get response status directly from database (no caching)
async function getVacancyResponseStatusFromDB(vacancyUrl) {
    try {
        console.log(`📡 Fetching response status for ${vacancyUrl} from database...`);
        const response = await fetch(`/Home/GetVacancyResponseStatus?vacancyUrl=${encodeURIComponent(vacancyUrl)}`);
        const data = await response.json();

        if (data.success) {
            console.log(`✅ Response status for ${vacancyUrl}: ${data.hasResponded}`);
            return data.hasResponded;
        } else {
            console.error('❌ Failed to get response status:', data.error);
            return false;
        }
    } catch (error) {
        console.error('❌ Error getting response status:', error);
        return false;
    }
}

// Save response status to API
async function saveResponseStatus(vacancy, hasResponded) {
    try {
        const vacancyUrl = vacancy.Url || vacancy.url;
        const requestData = {
            VacancyUrl: vacancyUrl,
            VacancyTitle: vacancy.Title || vacancy.title,
            CompanyName: vacancy.Company || vacancy.company,
            Notes: null
        };

        console.log(`💾 Saving response status for ${vacancyUrl}:`, requestData);

        const response = await fetch('/Home/ToggleVacancyResponse', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(requestData)
        });

        const data = await response.json();

        if (data.success) {
            console.log('✅ Response status saved:', data);
            return data.hasResponded;
        } else {
            console.error('❌ Failed to save response status:', data.error);
            return hasResponded;
        }
    } catch (error) {
        console.error('❌ Error saving response status:', error);
        return hasResponded;
    }
}

// Get vacancy unique ID (using URL as it's most reliable)
function getVacancyId(vacancy) {
    return vacancy.Url || vacancy.url || `${vacancy.Title}_${vacancy.Company}`.replace(/\s+/g, '_');
}

// Toggle response status for vacancy
async function toggleResponseStatus(vacancy) {
    const currentStatus = await getVacancyResponseStatusFromDB(vacancy.Url || vacancy.url);
    const newStatus = await saveResponseStatus(vacancy, !currentStatus);
    await refreshVacancyDisplay();
    return newStatus;
}

// Create response status button
async function createResponseButton(vacancy) {
    const vacancyUrl = vacancy.Url || vacancy.url;

    console.log(`🔘 Creating button for ${vacancy.Title || vacancy.title}`);

    const button = document.createElement('button');
    button.className = 'response-status-btn';
    button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Loading...';

    // Load initial status from database
    try {
        const isResponded = await getVacancyResponseStatusFromDB(vacancyUrl);
        button.className = `response-status-btn btn-${isResponded ? 'responded' : 'respond'}`;
        button.innerHTML = isResponded ?
            '<i class="fas fa-check me-1"></i>Responded' :
            '<i class="fas fa-paper-plane me-1"></i>Respond';
    } catch (error) {
        console.error('❌ Error loading initial status:', error);
        button.className = 'response-status-btn btn-respond';
        button.innerHTML = '<i class="fas fa-paper-plane me-1"></i>Respond';
    }

    button.onclick = async (e) => {
        e.preventDefault();
        e.stopPropagation();

        // Disable button during request
        button.disabled = true;
        const originalHTML = button.innerHTML;
        button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Saving...';

        try {
            const newStatus = await toggleResponseStatus(vacancy);

            // Update button based on new status
            button.className = `response-status-btn btn-${newStatus ? 'responded' : 'respond'}`;
            button.innerHTML = newStatus ?
                '<i class="fas fa-check me-1"></i>Responded' :
                '<i class="fas fa-paper-plane me-1"></i>Respond';
        } catch (error) {
            console.error('❌ Error toggling response status:', error);
            button.innerHTML = originalHTML;
        } finally {
            button.disabled = false;
        }
    };

    return button;
}

// Refresh vacancy display with current filter
async function refreshVacancyDisplay() {
    if (currentVacancies.length > 0) {
        await displayFilteredVacancies();
    }
}

// Filter vacancies based on current filter
async function getFilteredVacancies(vacancies) {
    if (currentFilter === 'all') {
        return vacancies;
    }

    const filteredVacancies = [];

    for (const match of vacancies) {
        const vacancy = match.Vacancy || match.vacancy;
        const hasResponded = await getVacancyResponseStatusFromDB(vacancy.Url || vacancy.url);

        if (currentFilter === 'responded' && hasResponded) {
            filteredVacancies.push(match);
        } else if (currentFilter === 'not_responded' && !hasResponded) {
            filteredVacancies.push(match);
        }
    }

    return filteredVacancies;
}

// Display filtered vacancies
async function displayFilteredVacancies() {
    const filteredVacancies = await getFilteredVacancies(currentVacancies);
    await updateVacancyTable(filteredVacancies);
}

// Update vacancy table with response status
async function updateVacancyTable(matches) {
    const tableBody = document.querySelector('#vacancyTable tbody');
    if (!tableBody) return;

    tableBody.innerHTML = '';

    for (let index = 0; index < matches.length; index++) {
        const match = matches[index];
        const vacancy = match.Vacancy || match.vacancy;
        const analysis = match.Analysis || match.analysis;

        if (!vacancy) continue;

        const normalizedVacancy = {
            Title: vacancy.Title || vacancy.title || 'Not specified',
            Company: vacancy.Company || vacancy.company || 'Not specified',
            Location: vacancy.Location || vacancy.location || 'Not specified',
            Experience: vacancy.Experience || vacancy.experience || 'Not specified',
            EnglishLevel: vacancy.EnglishLevel || vacancy.englishLevel || 'Not specified',
            Url: vacancy.Url || vacancy.url || '#'
        };

        console.log(`📝 Processing vacancy ${index + 1}: ${normalizedVacancy.Title}`);

        const matchScore = analysis?.MatchScore || analysis?.matchScore || 0;
        const formattedScore = Math.round(matchScore);

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${index + 1}</td>
            <td>${normalizedVacancy.Title}</td>
            <td>${normalizedVacancy.Company}</td>
            <td>${normalizedVacancy.Location}</td>
            <td>${normalizedVacancy.Experience}</td>
            <td>${normalizedVacancy.EnglishLevel}</td>
            <td><span class="badge bg-primary">${formattedScore}%</span></td>
            <td>
                <a href="${normalizedVacancy.Url}" target="_blank" class="btn btn-sm btn-outline-primary">
                    <i class="fas fa-external-link-alt me-1"></i>
                    ${window.localization?.viewVacancy || 'View'}
                </a>
            </td>
            <td></td>
        `;

        // Add response button to last cell
        const buttonCell = row.cells[row.cells.length - 1];
        const responseButton = await createResponseButton(vacancy);
        buttonCell.appendChild(responseButton);

        // Check if row should be marked as responded after button is created
        const isResponded = await getVacancyResponseStatusFromDB(normalizedVacancy.Url);
        if (isResponded) {
            row.classList.add('vacancy-responded');
        }

        tableBody.appendChild(row);
    }
}

// Setup filter buttons
function setupFilterButtons() {
    const filterAll = document.getElementById('filterAll');
    const filterResponded = document.getElementById('filterResponded');
    const filterNotResponded = document.getElementById('filterNotResponded');

    if (filterAll) {
        filterAll.addEventListener('click', async () => {
            currentFilter = 'all';
            updateFilterButtonState();
            await refreshVacancyDisplay();
        });
    }

    if (filterResponded) {
        filterResponded.addEventListener('click', async () => {
            currentFilter = 'responded';
            updateFilterButtonState();
            await refreshVacancyDisplay();
        });
    }

    if (filterNotResponded) {
        filterNotResponded.addEventListener('click', async () => {
            currentFilter = 'not_responded';
            updateFilterButtonState();
            await refreshVacancyDisplay();
        });
    }
}

// Update filter button states
function updateFilterButtonState() {
    const buttons = {
        'all': document.getElementById('filterAll'),
        'responded': document.getElementById('filterResponded'),
        'not_responded': document.getElementById('filterNotResponded')
    };

    Object.keys(buttons).forEach(key => {
        const button = buttons[key];
        if (button) {
            if (key === currentFilter) {
                button.classList.remove('btn-outline-primary', 'btn-outline-success', 'btn-outline-warning');
                if (key === 'all') button.classList.add('btn-primary');
                else if (key === 'responded') button.classList.add('btn-success');
                else button.classList.add('btn-warning');
            } else {
                button.classList.remove('btn-primary', 'btn-success', 'btn-warning');
                if (key === 'all') button.classList.add('btn-outline-primary');
                else if (key === 'responded') button.classList.add('btn-outline-success');
                else button.classList.add('btn-outline-warning');
            }
        }
    });
}

// Debug function to check database content
async function debugVacancyData() {
    try {
        const response = await fetch('/Home/DebugVacancyResponses');
        const data = await response.json();

        console.log('🐛 DEBUG: Database content:');
        console.log('  - Response count:', data.responseCount);
        console.log('  - Vacancy count:', data.vacancyCount);
        console.log('  - Sample responses:', data.responses);
        console.log('  - Sample vacancies:', data.vacancies);

        return data;
    } catch (error) {
        console.error('❌ Error debugging vacancy data:', error);
    }
}

// Initialize filter buttons when page loads
document.addEventListener('DOMContentLoaded', async function() {
    setupFilterButtons();
    updateFilterButtonState();

    // Debug database content first
    await debugVacancyData();

    console.log('📋 Page initialized - using real-time database queries for response status');
});

