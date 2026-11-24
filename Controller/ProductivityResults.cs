namespace Controller;

/// <summary>
/// Simple operation result with success status and message.
/// </summary>
public record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Generic operation result carrying data payload.
/// </summary>
public record OperationResult<T>(bool Success, T? Data = default, string? Error = null)
{
    public static OperationResult<T> Ok(T data) => new(true, data);
    public static OperationResult<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Board summary statistics calculated dynamically from persisted task items.
/// </summary>
public record BoardStatistics(
    int TotalTasks,
    int InProgressTasks,
    int CompletedTasks,
    double CompletionPercentage,
    int DueInNext7Days,
    int OverdueTasks
);

/// <summary>
/// Derived goal progress calculated dynamically from linked tasks.
/// </summary>
public record GoalProgressInfo(
    int CompletedTasks,
    int TotalTasks,
    double Percentage,
    bool HasTasks
);
