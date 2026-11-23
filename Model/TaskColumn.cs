namespace Model;

/// <summary>
/// A workflow column within a Kanban board.
/// </summary>
public class TaskColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the parent board.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Column name (e.g., To Do, In Progress, Done).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Zero-based display order within the board.</summary>
    public int Position { get; set; }

    /// <summary>Indicates whether tasks in this column are considered completed.</summary>
    public bool IsCompleted { get; set; }

    // Navigation properties
    public TaskBoard? Board { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
}
