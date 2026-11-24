using Microsoft.EntityFrameworkCore;
using Model;

namespace Controller;

/// <summary>
/// Service managing Kanban boards, columns, and task items with user isolation.
/// </summary>
public class BoardService
{
    private readonly Func<LifeBalanceDbContext> _dbFactory;
    private readonly ILocalDateProvider _dateProvider;

    public BoardService(Func<LifeBalanceDbContext>? dbFactory = null, ILocalDateProvider? dateProvider = null)
    {
        _dbFactory = dbFactory ?? (() => new LifeBalanceDbContext());
        _dateProvider = dateProvider ?? SystemLocalDateProvider.Instance;
    }

    #region Board Management

    /// <summary>
    /// Lists all boards belonging to the specified user.
    /// </summary>
    public async Task<List<TaskBoard>> GetUserBoardsAsync(Guid userId)
    {
        await using var db = _dbFactory();
        return await db.TaskBoards
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new board with default columns: 'To Do', 'In Progress', 'Done'.
    /// </summary>
    public async Task<OperationResult<TaskBoard>> CreateBoardAsync(Guid userId, string name, string? description = null)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<TaskBoard>.Fail("Board name cannot be empty.");

        var board = new TaskBoard
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = DateTime.UtcNow,
            Columns = new List<TaskColumn>
            {
                new() { Id = Guid.NewGuid(), Name = "To Do", Position = 0, IsCompleted = false },
                new() { Id = Guid.NewGuid(), Name = "In Progress", Position = 1, IsCompleted = false },
                new() { Id = Guid.NewGuid(), Name = "Done", Position = 2, IsCompleted = true }
            }
        };

        // Link parent ID to columns
        foreach (var col in board.Columns)
            col.BoardId = board.Id;

        await using var db = _dbFactory();
        db.TaskBoards.Add(board);
        await db.SaveChangesAsync();

        return OperationResult<TaskBoard>.Ok(board);
    }

    /// <summary>
    /// Retrieves a board and its columns and tasks ordered by position.
    /// </summary>
    public async Task<TaskBoard?> GetBoardDetailsAsync(Guid userId, Guid boardId)
    {
        await using var db = _dbFactory();
        var board = await db.TaskBoards
            .AsNoTracking()
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Tasks.OrderBy(t => t.Position))
                    .ThenInclude(t => t.TaskGoals)
                        .ThenInclude(tg => tg.Goal)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId);

        return board;
    }

    /// <summary>
    /// Updates board name and description.
    /// </summary>
    public async Task<OperationResult> UpdateBoardAsync(Guid userId, Guid boardId, string name, string? description)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Board name cannot be empty.");

        await using var db = _dbFactory();
        var board = await db.TaskBoards.FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId);
        if (board == null)
            return OperationResult.Fail("Board not found or access denied.");

        board.Name = name;
        board.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync();

        return OperationResult.Ok();
    }

    /// <summary>
    /// Deletes a board owned by the user, cascading deletion to columns and tasks.
    /// </summary>
    public async Task<OperationResult> DeleteBoardAsync(Guid userId, Guid boardId)
    {
        await using var db = _dbFactory();
        var board = await db.TaskBoards.FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId);
        if (board == null)
            return OperationResult.Fail("Board not found or access denied.");

        db.TaskBoards.Remove(board);
        await db.SaveChangesAsync();

        return OperationResult.Ok();
    }

    #endregion

    #region Column Management

    /// <summary>
    /// Adds a column to a board.
    /// </summary>
    public async Task<OperationResult<TaskColumn>> AddColumnAsync(Guid userId, Guid boardId, string name, bool isCompleted = false)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<TaskColumn>.Fail("Column name cannot be empty.");

        await using var db = _dbFactory();
        var board = await db.TaskBoards
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId);

        if (board == null)
            return OperationResult<TaskColumn>.Fail("Board not found or access denied.");

        var nextPosition = board.Columns.Count == 0 ? 0 : board.Columns.Max(c => c.Position) + 1;

        var column = new TaskColumn
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = name,
            Position = nextPosition,
            IsCompleted = isCompleted
        };

        db.TaskColumns.Add(column);
        await db.SaveChangesAsync();

        return OperationResult<TaskColumn>.Ok(column);
    }

    /// <summary>
    /// Renames a column and updates completion flag.
    /// </summary>
    public async Task<OperationResult> UpdateColumnAsync(Guid userId, Guid columnId, string name, bool isCompleted)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Column name cannot be empty.");

        await using var db = _dbFactory();
        var column = await db.TaskColumns
            .Include(c => c.Board)
            .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.Board != null && c.Board.UserId == userId);

        if (column == null)
            return OperationResult.Fail("Column not found or access denied.");

        bool wasCompleted = column.IsCompleted;
        column.Name = name;
        column.IsCompleted = isCompleted;

        // If completion status changed, update tasks in this column accordingly
        if (wasCompleted != isCompleted)
        {
            foreach (var task in column.Tasks)
            {
                task.CompletedAt = isCompleted ? DateTime.UtcNow : null;
            }
        }

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Deletes a column. Does not allow deleting the board's only column.
    /// If targetColumnIdForTasks is provided, tasks are moved there first.
    /// </summary>
    public async Task<OperationResult> DeleteColumnAsync(Guid userId, Guid columnId, Guid? targetColumnIdForTasks = null)
    {
        await using var db = _dbFactory();
        var column = await db.TaskColumns
            .Include(c => c.Board)
            .ThenInclude(b => b!.Columns)
            .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.Board != null && c.Board.UserId == userId);

        if (column == null)
            return OperationResult.Fail("Column not found or access denied.");

        if (column.Board!.Columns.Count <= 1)
            return OperationResult.Fail("Cannot delete the only column on a board.");

        if (column.Tasks.Count > 0)
        {
            if (!targetColumnIdForTasks.HasValue)
                return OperationResult.Fail("This column has tasks. Please select a target column to move them to.");

            var targetCol = await db.TaskColumns
                .Include(c => c.Tasks)
                .FirstOrDefaultAsync(c => c.Id == targetColumnIdForTasks.Value && c.BoardId == column.BoardId);

            if (targetCol == null || targetCol.Id == columnId)
                return OperationResult.Fail("Invalid target column selected.");

            int currentPosition = targetCol.Tasks.Count == 0 ? 0 : targetCol.Tasks.Max(t => t.Position) + 1;
            foreach (var task in column.Tasks)
            {
                task.ColumnId = targetCol.Id;
                task.Position = currentPosition++;
                task.CompletedAt = targetCol.IsCompleted ? (task.CompletedAt ?? DateTime.UtcNow) : null;
            }
        }

        db.TaskColumns.Remove(column);

        // Re-index remaining columns position
        var remaining = column.Board.Columns.Where(c => c.Id != columnId).OrderBy(c => c.Position).ToList();
        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i;
        }

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    #endregion

    #region Task Operations

    /// <summary>
    /// Creates a task card in the specified column.
    /// </summary>
    public async Task<OperationResult<TaskItem>> CreateTaskAsync(
        Guid userId,
        Guid columnId,
        string title,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateOnly? dueDate = null,
        TimeSpan? estimatedDuration = null)
    {
        title = title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return OperationResult<TaskItem>.Fail("Task title cannot be empty.");

        await using var db = _dbFactory();
        var column = await db.TaskColumns
            .Include(c => c.Board)
            .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.Id == columnId && c.Board != null && c.Board.UserId == userId);

        if (column == null)
            return OperationResult<TaskItem>.Fail("Column not found or access denied.");

        int nextPosition = column.Tasks.Count == 0 ? 0 : column.Tasks.Max(t => t.Position) + 1;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ColumnId = columnId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority,
            DueDate = dueDate,
            EstimatedDuration = estimatedDuration,
            Position = nextPosition,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = column.IsCompleted ? DateTime.UtcNow : null
        };

        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        return OperationResult<TaskItem>.Ok(task);
    }

    /// <summary>
    /// Updates an existing task's properties.
    /// </summary>
    public async Task<OperationResult> UpdateTaskAsync(
        Guid userId,
        Guid taskId,
        string title,
        string? description,
        TaskPriority priority,
        DateOnly? dueDate,
        TimeSpan? estimatedDuration)
    {
        title = title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return OperationResult.Fail("Task title cannot be empty.");

        await using var db = _dbFactory();
        var task = await db.TaskItems
            .Include(t => t.Column)
                .ThenInclude(c => c!.Board)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Column != null && t.Column.Board != null && t.Column.Board.UserId == userId);

        if (task == null)
            return OperationResult.Fail("Task not found or access denied.");

        task.Title = title;
        task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        task.Priority = priority;
        task.DueDate = dueDate;
        task.EstimatedDuration = estimatedDuration;

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Deletes a task card and re-numbers remaining tasks in the column.
    /// </summary>
    public async Task<OperationResult> DeleteTaskAsync(Guid userId, Guid taskId)
    {
        await using var db = _dbFactory();
        var task = await db.TaskItems
            .Include(t => t.Column)
                .ThenInclude(c => c!.Board)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Column != null && t.Column.Board != null && t.Column.Board.UserId == userId);

        if (task == null)
            return OperationResult.Fail("Task not found or access denied.");

        var columnId = task.ColumnId;
        db.TaskItems.Remove(task);

        // Renumber remaining tasks in the column
        var remaining = await db.TaskItems
            .Where(t => t.ColumnId == columnId && t.Id != taskId)
            .OrderBy(t => t.Position)
            .ToListAsync();

        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i;
        }

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Moves a task to another column or position with transaction safety and position renumbering.
    /// </summary>
    public async Task<OperationResult> MoveTaskAsync(Guid userId, Guid taskId, Guid targetColumnId, int targetPosition)
    {
        await using var db = _dbFactory();
        await using var tx = await db.Database.BeginTransactionAsync();

        // 1. Verify task ownership
        var task = await db.TaskItems
            .Include(t => t.Column)
                .ThenInclude(c => c!.Board)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.Column != null && t.Column.Board != null && t.Column.Board.UserId == userId);

        if (task == null)
            return OperationResult.Fail("Task not found or access denied.");

        // 2. Verify target column belongs to the same user and board
        var targetCol = await db.TaskColumns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == targetColumnId && c.BoardId == task.Column!.BoardId && c.Board!.UserId == userId);

        if (targetCol == null)
            return OperationResult.Fail("Target column not found or belongs to a different board.");

        var sourceColumnId = task.ColumnId;
        bool isChangingColumn = sourceColumnId != targetColumnId;

        // Fetch tasks in target column
        var targetColumnTasks = await db.TaskItems
            .Where(t => t.ColumnId == targetColumnId && t.Id != taskId)
            .OrderBy(t => t.Position)
            .ToListAsync();

        // Clamp target position
        targetPosition = Math.Clamp(targetPosition, 0, targetColumnTasks.Count);

        // Update task position and completion status
        task.ColumnId = targetColumnId;
        if (isChangingColumn)
        {
            task.CompletedAt = targetCol.IsCompleted ? (task.CompletedAt ?? DateTime.UtcNow) : null;
        }

        // Insert into target column list and renumber
        targetColumnTasks.Insert(targetPosition, task);
        for (int i = 0; i < targetColumnTasks.Count; i++)
        {
            targetColumnTasks[i].Position = i;
        }

        // If column changed, renumber source column tasks
        if (isChangingColumn)
        {
            var sourceColumnTasks = await db.TaskItems
                .Where(t => t.ColumnId == sourceColumnId && t.Id != taskId)
                .OrderBy(t => t.Position)
                .ToListAsync();

            for (int i = 0; i < sourceColumnTasks.Count; i++)
            {
                sourceColumnTasks[i].Position = i;
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return OperationResult.Ok();
    }

    #endregion

    #region Board Statistics

    /// <summary>
    /// Calculates live summary statistics for a board without storing computed columns.
    /// Semantics:
    /// - Columns with IsCompleted == true represent completed work.
    /// - The first non-completed column (ordered by Position) represents the backlog / To Do stage.
    /// - All other non-completed columns represent work in progress.
    /// </summary>
    public async Task<BoardStatistics> GetBoardStatisticsAsync(Guid userId, Guid boardId)
    {
        await using var db = _dbFactory();
        var board = await db.TaskBoards
            .AsNoTracking()
            .Include(b => b.Columns)
                .ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.UserId == userId);

        if (board == null)
            return new BoardStatistics(0, 0, 0, 0, 0, 0);

        var allTasks = board.Columns.SelectMany(c => c.Tasks).ToList();
        int total = allTasks.Count;

        // Completed tasks: all tasks in columns marked IsCompleted == true
        int completed = board.Columns
            .Where(c => c.IsCompleted)
            .SelectMany(c => c.Tasks)
            .Count();

        // Non-completed columns ordered by Position
        var nonCompletedColumns = board.Columns
            .Where(c => !c.IsCompleted)
            .OrderBy(c => c.Position)
            .ToList();

        // The first non-completed column represents the backlog / To Do stage.
        // Other non-completed columns represent work in progress.
        var firstNonCompletedColumnId = nonCompletedColumns.FirstOrDefault()?.Id;
        int inProgress = nonCompletedColumns
            .Where(c => c.Id != firstNonCompletedColumnId)
            .SelectMany(c => c.Tasks)
            .Count();

        double percentage = total == 0 ? 0 : Math.Round((double)completed / total * 100, 1);

        var today = _dateProvider.GetToday();
        var inSevenDays = today.AddDays(6); // today through today.AddDays(6), inclusive (7 calendar days)

        var nonCompletedTasks = nonCompletedColumns
            .SelectMany(c => c.Tasks)
            .ToList();

        int dueSoon = nonCompletedTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value >= today && t.DueDate.Value <= inSevenDays);
        int overdue = nonCompletedTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < today);

        return new BoardStatistics(total, inProgress, completed, percentage, dueSoon, overdue);
    }

    #endregion
}
