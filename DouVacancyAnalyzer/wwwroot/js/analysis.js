const connection = new signalR.HubConnectionBuilder()
    .withUrl("/analysishub")
    .build();

const startButton = document.getElementById('startAnalysis');
const startTestButton = document.getElementById('startTestAnalysis');
const cancelButton = document.getElementById('cancelAnalysis');
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

// Функція для нормалізації даних з урахуванням camelCase від SignalR
function normalizeDataKeys(data) {
    if (!data) return null;

    return {
        Report: data.Report || data.report,
        TechStats: data.TechStats || data.techStats,
        AiStats: data.AiStats || data.aiStats
    };
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

cancelButton.addEventListener('click', function() {
    if (analysisController) {
        analysisController.abort();
        cancelAnalysis();
    }
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

    setTimeout(() => {
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
            displayResults(normalizedData);
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

function displayResults(data) {
    console.log('displayResults called with data:', data);

    const { Report, TechStats, AiStats } = data;

    console.log('Report:', Report);
    console.log('TechStats:', TechStats);
    console.log('AiStats:', AiStats);

    if (!Report) {
        console.error('Report is missing');
        return;
    }

    if (!TechStats) {
        console.error('TechStats is missing');
        return;
    }

    try {
        displayQuickSummary(Report, TechStats);
        displayAnalysisStats(Report);
        displayTechStats(TechStats);
        displayModernTech(TechStats);
        displayVacancies(Report.Matches || []);
        displayCharts(TechStats, AiStats || {});
        console.log('All display functions completed successfully');
    } catch (error) {
        console.error('Error in displayResults:', error);
    }
}

function displayQuickSummary(report, techStats) {
    console.log('displayQuickSummary called with report:', report, 'techStats:', techStats);

    if (!report) {
        console.error('Report is null or undefined in displayQuickSummary');
        return;
    }

    if (!techStats) {
        console.error('TechStats is null or undefined in displayQuickSummary');
        return;
    }

    const container = document.getElementById('quickSummary');
    const matchPercentage = (report && typeof report.MatchPercentage === 'number') ? report.MatchPercentage : 0;
    const modernPercentage = (techStats && techStats.Total > 0) ? ((techStats.WithModernTech / techStats.Total) * 100).toFixed(1) : 0;

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
    console.log('displayAnalysisStats called with report:', report);

    const container = document.getElementById('analysisStats');
    const matches = report.Matches || [];
    const modernStackCount = matches.filter(m => m.Analysis && m.Analysis.IsModernStack).length;
    const middleLevelCount = matches.filter(m => m.Analysis && m.Analysis.IsMiddleLevel).length;
    const acceptableEnglishCount = matches.filter(m => m.Analysis && m.Analysis.HasAcceptableEnglish === true).length;
    const noTimeTrackerCount = matches.filter(m => m.Analysis && m.Analysis.HasNoTimeTracker !== false).length;

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
                        <small class="text-muted">Modern Stack</small>
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
    console.log('displayTechStats called with techStats:', techStats);

    const container = document.getElementById('techStats');

    container.innerHTML = `
        <div class="row g-3">
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-rocket fa-2x text-success"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${techStats.WithModernTech || 0}</h4>
                        <small class="text-muted">Modern Technologies</small>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="d-flex align-items-center p-3 bg-light rounded">
                    <div class="flex-shrink-0">
                        <i class="fas fa-trash-alt fa-2x text-danger"></i>
                    </div>
                    <div class="flex-grow-1 ms-3">
                        <h4 class="mb-0">${techStats.WithOutdatedTech || 0}</h4>
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
                        <h4 class="mb-0">${techStats.WithDesktopApps || 0}</h4>
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
                        <h4 class="mb-0">${techStats.WithFrontend || 0}</h4>
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
                        <h4 class="mb-0">${techStats.WithTimeTracker || 0}</h4>
                        <small class="text-muted">With Time Tracker</small>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function displayModernTech(techStats) {
    console.log('displayModernTech called with techStats:', techStats);

    const container = document.getElementById('modernTech');
    const modernTechCount = techStats.ModernTechCount || {};
    const topTech = Object.entries(modernTechCount)
        .sort(([,a], [,b]) => b - a)
        .slice(0, 15);

    if (topTech.length === 0) {
        container.innerHTML = '<div class="text-center text-muted py-4"><i class="fas fa-info-circle fa-2x mb-2"></i><p>No modern technology data available</p></div>';
        return;
    }

    container.innerHTML = `
        <div class="row g-2">
            ${topTech.map(([tech, count]) => `
                <div class="col-md-4 col-sm-6">
                    <div class="badge bg-primary p-3 d-flex justify-content-between align-items-center w-100">
                        <span class="fw-bold">${tech}</span>
                        <span class="badge bg-light text-dark">${count}</span>
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

function displayVacancies(matches) {
    console.log('displayVacancies called with matches:', matches);

    const tbody = document.querySelector('#vacancyTable tbody');
    const matchesList = matches || [];
    const top20 = matchesList.slice(0, 20);

    if (top20.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">No matching vacancies found</td></tr>';
        return;
    }

    tbody.innerHTML = top20.map((match, index) => {
        const vacancy = match.Vacancy || {};
        const analysis = match.Analysis || {};
        const score = (analysis.MatchScore || 0).toFixed(0);
        const scoreClass = score >= 80 ? 'text-success' : score >= 60 ? 'text-warning' : 'text-danger';

        return `
            <tr class="align-middle">
                <td><span class="badge bg-secondary">${index + 1}</span></td>
                <td class="fw-bold">${vacancy.Title || 'Not specified'}</td>
                <td>${vacancy.Company || 'Not specified'}</td>
                <td><i class="fas fa-map-marker-alt text-muted me-1"></i>${vacancy.Location || 'Not specified'}</td>
                <td><span class="badge bg-info">${vacancy.Experience || 'Not specified'}</span></td>
                <td><span class="badge bg-success">${vacancy.EnglishLevel || 'Not specified'}</span></td>
                <td><span class="fw-bold ${scoreClass}">${score}%</span></td>
                <td>
                    <a href="${vacancy.Url || '#'}" target="_blank" class="btn btn-outline-primary btn-sm">
                        <i class="fas fa-external-link-alt me-1"></i>View
                    </a>
                </td>
            </tr>
        `;
    }).join('');
}

function displayCharts(techStats, aiStats) {
    const expCtx = document.getElementById('experienceChart').getContext('2d');
    if (experienceChart) {
        experienceChart.destroy();
    }

    experienceChart = new Chart(expCtx, {
        type: 'doughnut',
        data: {
            labels: ['Junior', 'Middle', 'Senior', 'Не вказано'],
            datasets: [{
                data: [
                    techStats.JuniorLevel,
                    techStats.MiddleLevel,
                    techStats.SeniorLevel,
                    techStats.UnspecifiedLevel
                ],
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

    if (aiStats.VacancyCategories && Object.keys(aiStats.VacancyCategories).length > 0) {
        const catCtx = document.getElementById('categoryChart').getContext('2d');
        if (categoryChart) {
            categoryChart.destroy();
        }

        categoryChart = new Chart(catCtx, {
            type: 'bar',
            data: {
                labels: Object.keys(aiStats.VacancyCategories),
                datasets: [{
                    label: 'Кількість вакансій',
                    data: Object.values(aiStats.VacancyCategories),
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
    }
}