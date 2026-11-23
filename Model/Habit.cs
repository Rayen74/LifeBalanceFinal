namespace Model;

/// <summary>
/// A recurring habit definition tracked by a user.
/// </summary>
public class Habit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the owner user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Habit name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional habit description.</summary>
    public string? Description { get; set; }

    /// <summary>Recurring frequency (Daily, Weekly).</summary>
    public HabitFrequency Frequency { get; set; } = HabitFrequency.Daily;

    /// <summary>Start date from which completions can be logged.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Whether the habit is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public List<HabitCompletion> Completions { get; set; } = new();
}
