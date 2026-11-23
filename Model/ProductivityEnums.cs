namespace Model;

/// <summary>
/// Task priority levels.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Goal status levels.
/// </summary>
public enum GoalStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2
}

/// <summary>
/// Habit recurring frequency.
/// </summary>
public enum HabitFrequency
{
    Daily = 0,
    Weekly = 1
}
