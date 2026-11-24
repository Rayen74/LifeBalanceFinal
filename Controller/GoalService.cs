using Microsoft.EntityFrameworkCore;
using Model;

namespace Controller;

/// <summary>
/// Service managing personal goals and task-goal links with user isolation.
/// </summary>
public class GoalService
{
    private readonly Func<LifeBalanceDbContext> _dbFactory;

    public GoalService(Func<LifeBalanceDbContext>? dbFactory = null)
    {
        _dbFactory = dbFactory ?? (() => new LifeBalanceDbContext());
    }

    /// <summary>
    /// Lists all goals belonging to the user, including linked tasks and their column status for derived progress calculation.
    /// </summary>
    public async Task<List<PersonalGoal>> GetUserGoalsAsync(Guid userId)
    {
        await using var db = _dbFactory();
        return await db.PersonalGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .Include(g => g.TaskGoals)
                .ThenInclude(tg => tg.Task)
                    .ThenInclude(t => t!.Column)
            .OrderBy(g => g.Status)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all tasks belonging to the user across all boards, including column and board details.
    /// </summary>
    public async Task<List<TaskItem>> GetUserTasksAsync(Guid userId)
    {
        await using var db = _dbFactory();
        return await db.TaskItems
            .AsNoTracking()
            .Include(t => t.Column)
                .ThenInclude(c => c!.Board)
            .Where(t => t.Column != null && t.Column.Board != null && t.Column.Board.UserId == userId)
            .OrderBy(t => t.Column!.Board!.Name)
            .ThenBy(t => t.Column!.Position)
            .ThenBy(t => t.Position)
            .ToListAsync();
    }

    /// <summary>
    /// Computes derived goal progress from linked tasks:
    /// (CompletedLinkedTasks / TotalLinkedTasks) * 100.
    /// A task is completed when its current column has IsCompleted == true.
    /// </summary>
    public static GoalProgressInfo CalculateGoalProgress(PersonalGoal goal)
    {
        var validTasks = goal.TaskGoals
            .Where(tg => tg.Task != null)
            .Select(tg => tg.Task!)
            .ToList();

        if (validTasks.Count == 0)
        {
            return new GoalProgressInfo(0, 0, 0, false);
        }

        int completedCount = validTasks.Count(t => t.Column != null && t.Column.IsCompleted);
        double percentage = Math.Round((double)completedCount / validTasks.Count * 100, 1);
        return new GoalProgressInfo(completedCount, validTasks.Count, percentage, true);
    }

    /// <summary>
    /// Creates a new personal goal.
    /// </summary>
    public async Task<OperationResult<PersonalGoal>> CreateGoalAsync(
        Guid userId,
        string name,
        string? description = null,
        DateOnly? targetDate = null,
        GoalStatus status = GoalStatus.Active)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<PersonalGoal>.Fail("Goal name cannot be empty.");

        var goal = new PersonalGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            TargetDate = targetDate,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = _dbFactory();
        db.PersonalGoals.Add(goal);
        await db.SaveChangesAsync();

        return OperationResult<PersonalGoal>.Ok(goal);
    }

    /// <summary>
    /// Updates goal properties.
    /// </summary>
    public async Task<OperationResult> UpdateGoalAsync(
        Guid userId,
        Guid goalId,
        string name,
        string? description,
        DateOnly? targetDate,
        GoalStatus status)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Goal name cannot be empty.");

        await using var db = _dbFactory();
        var goal = await db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
        if (goal == null)
            return OperationResult.Fail("Goal not found or access denied.");

        goal.Name = name;
        goal.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        goal.TargetDate = targetDate;
        goal.Status = status;

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Deletes a goal and automatically removes task-goal links.
    /// </summary>
    public async Task<OperationResult> DeleteGoalAsync(Guid userId, Guid goalId)
    {
        await using var db = _dbFactory();
        var goal = await db.PersonalGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);
        if (goal == null)
            return OperationResult.Fail("Goal not found or access denied.");

        db.PersonalGoals.Remove(goal);
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Links a task to a goal after verifying that both belong to the current authenticated user.
    /// </summary>
    public async Task<OperationResult> LinkTaskToGoalAsync(Guid userId, Guid taskId, Guid goalId)
    {
        await using var db = _dbFactory();

        // 1. Verify task ownership
        var task = await db.TaskItems
            .Include(t => t.Column)
                .ThenInclude(c => c!.Board)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Column != null && t.Column.Board != null && t.Column.Board.UserId == userId);

        if (task == null)
            return OperationResult.Fail("Task not found or access denied.");

        // 2. Verify goal ownership
        var goal = await db.PersonalGoals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId);

        if (goal == null)
            return OperationResult.Fail("Goal not found or access denied.");

        // 3. Check for existing link
        bool exists = await db.TaskGoals.AnyAsync(tg => tg.TaskId == taskId && tg.GoalId == goalId);
        if (exists)
            return OperationResult.Fail("This task is already linked to the specified goal.");

        var link = new TaskGoal
        {
            TaskId = taskId,
            GoalId = goalId
        };

        db.TaskGoals.Add(link);
        await db.SaveChangesAsync();

        return OperationResult.Ok();
    }

    /// <summary>
    /// Unlinks a task from a goal after verifying ownership.
    /// </summary>
    public async Task<OperationResult> UnlinkTaskFromGoalAsync(Guid userId, Guid taskId, Guid goalId)
    {
        await using var db = _dbFactory();

        var link = await db.TaskGoals
            .Include(tg => tg.Goal)
            .FirstOrDefaultAsync(tg => tg.TaskId == taskId && tg.GoalId == goalId && tg.Goal != null && tg.Goal.UserId == userId);

        if (link == null)
            return OperationResult.Fail("Link not found or access denied.");

        db.TaskGoals.Remove(link);
        await db.SaveChangesAsync();

        return OperationResult.Ok();
    }
}
