namespace Model;

/// <summary>
/// A Kanban board owned by a user.
/// </summary>
public class TaskBoard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to the owner user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Required board name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional board description.</summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the board was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public List<TaskColumn> Columns { get; set; } = new();
}
