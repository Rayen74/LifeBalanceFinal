namespace Model;

/// <summary>
/// Join entity for many-to-many relationship between TaskItem and PersonalGoal.
/// </summary>
public class TaskGoal
{
    public Guid TaskId { get; set; }
    public TaskItem? Task { get; set; }

    public Guid GoalId { get; set; }
    public PersonalGoal? Goal { get; set; }
}
