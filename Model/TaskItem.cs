namespace Model;

/// <summary>
/// A task card placed within a Kanban column.
/// </summary>
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the parent column.</summary>
    public Guid ColumnId { get; set; }

    /// <summary>Optional category foreign key (reserved for shared categories).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Required title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional task description.</summary>
    public string? Description { get; set; }

    /// <summary>Task priority (Low, Medium, High).</summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Optional due date.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional estimated duration.</summary>
    public TimeSpan? EstimatedDuration { get; set; }

    /// <summary>Zero-based order inside the column.</summary>
    public int Position { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when placed in a completed column, cleared when moved out.</summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public TaskColumn? Column { get; set; }
    public List<TaskGoal> TaskGoals { get; set; } = new();
}
