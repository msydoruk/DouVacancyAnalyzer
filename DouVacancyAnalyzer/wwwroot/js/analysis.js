const connection = new signalR.HubConnectionBuilder()
    .withUrl("/analysishub")
    .build();

const startButton = document.getElementById('startAnalysis');
const startTestButton = document.getElementById('startTestAnalysis');
const progressSection = document.getElementById('progressSection');
const resultsSection = document.getElementById('resultsSection');
const progressBar = document.getElementById('progressBar');
const progressMessage = document.getElementById('progressMessage');
const progressLog = document.getElementById('progressLog');
const progressLogContent = document.getElementById('progressLogContent');

let experienceChart = null;
let categoryChart = null;
let currentLogEntry = null;
connection.start().then(function () {
    console.log('SignalR connection established');
}).catch(function (err) {
    console.error('SignalR connection error: ', err.toString());
});

startButton.addEventListener('click', function() {
    startAnalysis();
});

startTestButton.addEventListener('click', function() {
    startTestAnalysis();
});

connection.on("AnalysisStarted", function () {
    startButton.disabled = true;
    startTestButton.disabled = true;
    startButton.innerHTML = `<i class="fas fa-spinner fa-spin me-2"></i>${window.localization.analysisInProgress}`;
    startTestButton.innerHTML = `<i class="fas fa-spinner fa-spin me-2"></i>${window.localization.analysisInProgress}`;
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
    updateProgress(100, `✅ ${window.localization.analysisCompleted}!`);

    if (currentLogEntry) {
        currentLogEntry.classList.remove('current');
        currentLogEntry.classList.add('completed');
    }

    addLogEntry(window.localization.analysisCompleted, 'completed');

    setTimeout(() => {
        progressSection.style.display = 'none';
        resultsSection.style.display = 'block';
        displayResults(data);
        resetButton();
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
    resetButton();
});

function startAnalysis() {
    fetch('/Home/StartAnalysis', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        }
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            alert('Помилка запуску аналізу: ' + data.error);
            resetButton();
        }
    })
    .catch(error => {
        console.error('Error:', error);
        alert('Помилка запуску аналізу');
        resetButton();
    });
}

function startTestAnalysis() {
    fetch('/Home/StartTestAnalysis', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        }
    })
    .then(response => response.json())
    .then(data => {
        if (!data.success) {
            alert('Помилка запуску тестового аналізу: ' + data.error);
            resetButton();
        }
    })
    .catch(error => {
        console.error('Error:', error);
        alert('Помилка запуску тестового аналізу');
        resetButton();
    });
}

function updateProgress(progress, message) {
    progressBar.style.width = progress + '%';
    progressBar.setAttribute('aria-valuenow', progress);
    progressBar.textContent = progress + '%';
    progressMessage.textContent = message;
}

function resetButton() {
    startButton.disabled = false;
    startTestButton.disabled = false;
    startButton.innerHTML = `<i class="fas fa-play me-2"></i>${window.localization.startAnalysis}`;
    startTestButton.innerHTML = `<i class="fas fa-flask me-2"></i>${window.localization.startTestAnalysis}`;
    progressBar.classList.remove('bg-danger');
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

function displayResults(data) {
    const { Report, TechStats, AiStats } = data;

    displayQuickSummary(Report, TechStats);

    displayAnalysisStats(Report);

    displayTechStats(TechStats);

    displayModernTech(TechStats);

    displayVacancies(Report.Matches);

    displayCharts(TechStats, AiStats);
}

function displayQuickSummary(report, techStats) {
    const container = document.getElementById('quickSummary');
    const matchPercentage = report.MatchPercentage;
    const modernPercentage = ((techStats.WithModernTech / techStats.Total) * 100).toFixed(1);

    container.innerHTML = `
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-primary">${report.TotalVacancies}</div>
                <div class="summary-label">${window.localization.totalVacancies}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-success">${report.MatchingVacancies}</div>
                <div class="summary-label">${window.localization.matching}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-warning">${matchPercentage.toFixed(1)}%</div>
                <div class="summary-label">${window.localization.compliance}</div>
            </div>
        </div>
        <div class="col-md-3 text-center">
            <div class="summary-card">
                <div class="summary-number text-info">${modernPercentage}%</div>
                <div class="summary-label">${window.localization.modernTech}</div>
            </div>
        </div>
    `;
}

function displayAnalysisStats(report) {
    const container = document.getElementById('analysisStats');
    const modernStackCount = report.Matches.filter(m => m.Analysis.IsModernStack).length;
    const middleLevelCount = report.Matches.filter(m => m.Analysis.IsMiddleLevel).length;
    const acceptableEnglishCount = report.Matches.filter(m => m.Analysis.HasAcceptableEnglish === true).length;
    const noTimeTrackerCount = report.Matches.filter(m => m.Analysis.HasNoTimeTracker !== false).length;

    container.innerHTML = `
        <div class="stat-item">
            <div class="stat-value">${report.TotalVacancies}</div>
            <div class="stat-label">📊 Всього вакансій</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${report.MatchingVacancies}</div>
            <div class="stat-label">✅ Відповідають критеріям</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${report.MatchPercentage.toFixed(1)}%</div>
            <div class="stat-label">📈 Відсоток відповідності</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${modernStackCount}</div>
            <div class="stat-label">🔧 З сучасним стеком</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${middleLevelCount}</div>
            <div class="stat-label">👔 Middle рівень</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${acceptableEnglishCount}</div>
            <div class="stat-label">🌐 Прийнятна англійська</div>
        </div>
    `;
}

function displayTechStats(techStats) {
    const container = document.getElementById('techStats');

    container.innerHTML = `
        <div class="stat-item">
            <div class="stat-value">${techStats.WithModernTech}</div>
            <div class="stat-label">🚀 З сучасними технологіями</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${techStats.WithOutdatedTech}</div>
            <div class="stat-label">🗑️ З застарілими технологіями</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${techStats.WithDesktopApps}</div>
            <div class="stat-label">🖥️ Desktop додатки</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${techStats.WithFrontend}</div>
            <div class="stat-label">🎨 Frontend позиції</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">${techStats.WithTimeTracker}</div>
            <div class="stat-label">⏱️ З time tracker</div>
        </div>
    `;
}

function displayModernTech(techStats) {
    const container = document.getElementById('modernTech');
    const topTech = Object.entries(techStats.ModernTechCount)
        .sort(([,a], [,b]) => b - a)
        .slice(0, 15);

    container.innerHTML = topTech.map(([tech, count]) =>
        `<span class="tech-item">${tech} (${count})</span>`
    ).join('');
}

function displayVacancies(matches) {
    const tbody = document.querySelector('#vacancyTable tbody');
    const top20 = matches.slice(0, 20);

    tbody.innerHTML = top20.map((match, index) => `
        <tr>
            <td>${index + 1}</td>
            <td>${match.Vacancy.Title}</td>
            <td>${match.Vacancy.Company}</td>
            <td>${match.Vacancy.Location}</td>
            <td><span class="badge bg-info badge-experience">${match.Vacancy.Experience}</span></td>
            <td><span class="badge bg-success badge-experience">${match.Vacancy.EnglishLevel}</span></td>
            <td><strong>${match.Analysis.MatchScore.toFixed(0)}</strong></td>
            <td><a href="${match.Vacancy.Url}" target="_blank" class="vacancy-link">🔗 Переглянути</a></td>
        </tr>
    `).join('');
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