namespace Model;

/// <summary>
/// A personal goal that can be linked to multiple tasks.
/// </summary>
public class PersonalGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the owner user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Required goal name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional goal description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional target completion date.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>Goal status (Active, Completed, Cancelled).</summary>
    public GoalStatus Status { get; set; } = GoalStatus.Active;

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public List<TaskGoal> TaskGoals { get; set; } = new();
}
