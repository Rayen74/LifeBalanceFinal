using Model;

namespace Controller;

/// <summary>Business logic for the dashboard window. The View only talks to this class.</summary>
public class DashboardController
{
    public IReadOnlyList<LifeArea> Areas { get; } = new List<LifeArea>
    {
        new("Health", "💪", 7),
        new("Career", "💼", 6),
        new("Finances", "💰", 5),
        new("Relationships", "❤️", 8),
        new("Personal growth", "📚", 6),
        new("Fun & leisure", "🎮", 4),
    };

    /// <summary>Average score of all areas, 0 to 10, one decimal.</summary>
    public double BalanceScore =>
        Areas.Count == 0 ? 0 : Math.Round(Areas.Average(a => a.Score), 1);

    public string BalanceMessage => BalanceScore switch
    {
        >= 8 => "You're thriving — keep it up.",
        >= 6 => "Pretty balanced. Small tweaks will go a long way.",
        >= 4 => "Some areas need attention.",
        _ => "Time to refocus on what matters.",
    };

    /// <summary>Tries to reach PostgreSQL using the Model's DbContext.</summary>
    public async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            await using var db = new LifeBalanceDbContext();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await db.Database.CanConnectAsync(cts.Token);
        }
        catch
        {
            return false;
        }
    }
}
