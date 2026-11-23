namespace Model;

/// <summary>
/// A single recorded habit completion on a specific calendar date.
/// </summary>
public class HabitCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the completed habit.</summary>
    public Guid HabitId { get; set; }

    /// <summary>Date of completion.</summary>
    public DateOnly CompletedOn { get; set; }

    // Navigation property
    public Habit? Habit { get; set; }
}
