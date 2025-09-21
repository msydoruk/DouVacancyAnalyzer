using Microsoft.AspNetCore.SignalR;

namespace DouVacancyAnalyzer.Hubs;

public class AnalysisHub : Hub
{
    public async Task JoinAnalysisGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AnalysisGroup");
    }

    public async Task LeaveAnalysisGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AnalysisGroup");
    }
}