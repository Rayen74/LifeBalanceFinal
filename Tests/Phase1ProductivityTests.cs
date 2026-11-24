using Controller;
using Microsoft.EntityFrameworkCore;
using Model;
using Xunit;

namespace Tests;

/// <summary>
/// Unit and integration tests for Phase 1 Productivity Workflow Improvements:
/// - Drag-and-drop position calculation and task movement
/// - Goal-task linking and derived progress
/// - Local date provider and board statistics accuracy
/// </summary>
public class Phase1ProductivityTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LIFEBALANCE_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=lifebalancedb_test;Username=postgres;Password=12345";

    private static readonly DbContextOptions<LifeBalanceDbContext> Options =
        new DbContextOptionsBuilder<LifeBalanceDbContext>().UseNpgsql(ConnectionString).Options;

    private static LifeBalanceDbContext NewDb() => new(Options);

    private static async Task<User> CreateTestUserAsync(string prefix = "p1")
    {
        await using var db = NewDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{prefix}-{Guid.NewGuid():N}@example.com",
            PasswordHash = "$2a$11$somethingvalid"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    #region A. Kanban Pure Position & Movement Tests

    [Fact]
    public void KanbanPositionHelper_ReorderWithinSameColumn_CalculatesAccurately()
    {
        var colId = Guid.NewGuid();

        // 3 items: [0, 1, 2]
        // 1. Move task at pos 2 to top before card at pos 0 (dropAfter: false) -> 0
        int pos1 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 2, 0, false, 3);
        Assert.Equal(0, pos1);

        // 2. Move task at pos 2 to after card at pos 0 (dropAfter: true) -> 1
        int pos2 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 2, 0, true, 3);
        Assert.Equal(1, pos2);

        // 3. Move task at pos 0 to after card at pos 1 (dropAfter: true) -> in list [1, 2], desired is index 1+1=2, adjusted -1 = 1
        int pos3 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 0, 1, true, 3);
        Assert.Equal(1, pos3);

        // 4. Move task at pos 0 to after card at pos 2 (dropAfter: true) -> bottom of column -> 2
        int pos4 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 0, 2, true, 3);
        Assert.Equal(2, pos4);

        // 5. Drop task back to original position (pos 1 dropped before pos 1) -> 1
        int pos5 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 1, 1, false, 3);
        Assert.Equal(1, pos5);

        // 6. Dropped onto column empty space in same column -> last position (2)
        int pos6 = KanbanPositionHelper.CalculateDropPosition(colId, colId, 0, null, false, 3);
        Assert.Equal(2, pos6);
    }

    [Fact]
    public void KanbanPositionHelper_CrossColumn_CalculatesAccurately()
    {
        var colA = Guid.NewGuid();
        var colB = Guid.NewGuid();

        // Destination has 2 items: [0, 1]. Total 2.
        // 1. Drop before first card (pos 0, dropAfter: false) -> 0
        int p1 = KanbanPositionHelper.CalculateDropPosition(colA, colB, 0, 0, false, 2);
        Assert.Equal(0, p1);

        // 2. Drop after first card (pos 0, dropAfter: true) -> 1
        int p2 = KanbanPositionHelper.CalculateDropPosition(colA, colB, 0, 0, true, 2);
        Assert.Equal(1, p2);

        // 3. Drop after second card (pos 1, dropAfter: true) -> 2 (appended to end)
        int p3 = KanbanPositionHelper.CalculateDropPosition(colA, colB, 0, 1, true, 2);
        Assert.Equal(2, p3);

        // 4. Drop into empty column (target count 0, targetCardPosition null) -> 0
        int p4 = KanbanPositionHelper.CalculateDropPosition(colA, colB, 0, null, false, 0);
        Assert.Equal(0, p4);

        // 5. Drop onto column empty space with 2 existing items -> append at index 2
        int p5 = KanbanPositionHelper.CalculateDropPosition(colA, colB, 0, null, false, 2);
        Assert.Equal(2, p5);
    }

    [Fact]
    public async Task MoveTask_FullLifecycle_AndConsecutivePositions()
    {
        var user = await CreateTestUserAsync("lifecycle");
        var service = new BoardService(NewDb);
        var board = (await service.CreateBoardAsync(user.Id, "Kanban Flow")).Data!;

        var todo = board.Columns.First(c => c.Name == "To Do");
        var inProg = board.Columns.First(c => c.Name == "In Progress");
        var done = board.Columns.First(c => c.Name == "Done");

        var t1 = (await service.CreateTaskAsync(user.Id, todo.Id, "Task 1")).Data!;
        var t2 = (await service.CreateTaskAsync(user.Id, todo.Id, "Task 2")).Data!;
        var t3 = (await service.CreateTaskAsync(user.Id, todo.Id, "Task 3")).Data!;

        // 1. Move Task 1 to In Progress
        var m1 = await service.MoveTaskAsync(user.Id, t1.Id, inProg.Id, 0);
        Assert.True(m1.Success);

        // 2. Move Task 1 from In Progress to Done (sets CompletedAt)
        var m2 = await service.MoveTaskAsync(user.Id, t1.Id, done.Id, 0);
        Assert.True(m2.Success);

        var details = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var doneTask = details!.Columns.First(c => c.Id == done.Id).Tasks.First();
        Assert.NotNull(doneTask.CompletedAt);

        // 3. Move Task 1 from Done back to active column (clears CompletedAt)
        var m3 = await service.MoveTaskAsync(user.Id, t1.Id, inProg.Id, 0);
        Assert.True(m3.Success);

        details = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var reactivatedTask = details!.Columns.First(c => c.Id == inProg.Id).Tasks.First();
        Assert.Null(reactivatedTask.CompletedAt);

        // 4. Reorder tasks in To Do: move Task 3 to position 0
        var m4 = await service.MoveTaskAsync(user.Id, t3.Id, todo.Id, 0);
        Assert.True(m4.Success);

        details = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var todoTasks = details!.Columns.First(c => c.Id == todo.Id).Tasks.OrderBy(t => t.Position).ToList();
        Assert.Equal(2, todoTasks.Count);
        Assert.Equal(t3.Id, todoTasks[0].Id);
        Assert.Equal(0, todoTasks[0].Position);
        Assert.Equal(t2.Id, todoTasks[1].Id);
        Assert.Equal(1, todoTasks[1].Position);
    }

    [Fact]
    public async Task MoveTask_CrossBoard_AndCrossUser_AreRejected()
    {
        var userA = await CreateTestUserAsync("mvA");
        var userB = await CreateTestUserAsync("mvB");
        var service = new BoardService(NewDb);

        var boardA1 = (await service.CreateBoardAsync(userA.Id, "Board A1")).Data!;
        var boardA2 = (await service.CreateBoardAsync(userA.Id, "Board A2")).Data!;
        var boardB = (await service.CreateBoardAsync(userB.Id, "Board B")).Data!;

        var taskA = (await service.CreateTaskAsync(userA.Id, boardA1.Columns[0].Id, "Task on A1")).Data!;

        // Cross-board within same user is rejected
        var crossBoardResult = await service.MoveTaskAsync(userA.Id, taskA.Id, boardA2.Columns[0].Id, 0);
        Assert.False(crossBoardResult.Success);

        // Cross-user move is rejected
        var crossUserResult = await service.MoveTaskAsync(userB.Id, taskA.Id, boardB.Columns[0].Id, 0);
        Assert.False(crossUserResult.Success);
    }

    #endregion

    #region B. Goal-Task Linking & Derived Progress Tests

    [Fact]
    public async Task GoalService_LinkUnlink_AndDuplicateRejection()
    {
        var user = await CreateTestUserAsync("link");
        var boardService = new BoardService(NewDb);
        var goalService = new GoalService(NewDb);

        var board = (await boardService.CreateBoardAsync(user.Id, "Main Board")).Data!;
        var task = (await boardService.CreateTaskAsync(user.Id, board.Columns[0].Id, "Write Thesis")).Data!;
        var goal = (await goalService.CreateGoalAsync(user.Id, "Master Degree")).Data!;

        // Link
        var linkRes = await goalService.LinkTaskToGoalAsync(user.Id, task.Id, goal.Id);
        Assert.True(linkRes.Success);

        // Duplicate link rejected
        var dupRes = await goalService.LinkTaskToGoalAsync(user.Id, task.Id, goal.Id);
        Assert.False(dupRes.Success);

        // Unlink
        var unlinkRes = await goalService.UnlinkTaskFromGoalAsync(user.Id, task.Id, goal.Id);
        Assert.True(unlinkRes.Success);

        // Unlink again rejected
        var unlinkAgain = await goalService.UnlinkTaskFromGoalAsync(user.Id, task.Id, goal.Id);
        Assert.False(unlinkAgain.Success);
    }

    [Fact]
    public async Task GetUserTasksAsync_RetrievesAcrossMultipleBoards_AndIsolatesUsers()
    {
        var userA = await CreateTestUserAsync("userTasksA");
        var userB = await CreateTestUserAsync("userTasksB");
        var boardService = new BoardService(NewDb);
        var goalService = new GoalService(NewDb);

        var boardA1 = (await boardService.CreateBoardAsync(userA.Id, "A1")).Data!;
        var boardA2 = (await boardService.CreateBoardAsync(userA.Id, "A2")).Data!;
        var boardB = (await boardService.CreateBoardAsync(userB.Id, "B1")).Data!;

        await boardService.CreateTaskAsync(userA.Id, boardA1.Columns[0].Id, "Task A1");
        await boardService.CreateTaskAsync(userA.Id, boardA2.Columns[0].Id, "Task A2");
        await boardService.CreateTaskAsync(userB.Id, boardB.Columns[0].Id, "Task B1");

        var tasksA = await goalService.GetUserTasksAsync(userA.Id);
        Assert.Equal(2, tasksA.Count);
        Assert.Contains(tasksA, t => t.Title == "Task A1");
        Assert.Contains(tasksA, t => t.Title == "Task A2");
        Assert.DoesNotContain(tasksA, t => t.Title == "Task B1");
    }

    [Fact]
    public async Task DerivedGoalProgress_CalculatesCorrectly_AndRecalculatesOnColumnMove()
    {
        var user = await CreateTestUserAsync("progress");
        var boardService = new BoardService(NewDb);
        var goalService = new GoalService(NewDb);

        var board = (await boardService.CreateBoardAsync(user.Id, "Project")).Data!;
        var todo = board.Columns.First(c => c.Name == "To Do");
        var done = board.Columns.First(c => c.Name == "Done");

        var task1 = (await boardService.CreateTaskAsync(user.Id, todo.Id, "Task 1")).Data!;
        var task2 = (await boardService.CreateTaskAsync(user.Id, todo.Id, "Task 2")).Data!;
        var goal = (await goalService.CreateGoalAsync(user.Id, "Ship Project")).Data!;

        // 1. Goal with no linked tasks
        var goals = await goalService.GetUserGoalsAsync(user.Id);
        var p0 = GoalService.CalculateGoalProgress(goals[0]);
        Assert.False(p0.HasTasks);
        Assert.Equal(0, p0.TotalTasks);
        Assert.Equal(0, p0.CompletedTasks);

        // 2. Link Task 1 and Task 2
        await goalService.LinkTaskToGoalAsync(user.Id, task1.Id, goal.Id);
        await goalService.LinkTaskToGoalAsync(user.Id, task2.Id, goal.Id);

        goals = await goalService.GetUserGoalsAsync(user.Id);
        var p1 = GoalService.CalculateGoalProgress(goals[0]);
        Assert.True(p1.HasTasks);
        Assert.Equal(2, p1.TotalTasks);
        Assert.Equal(0, p1.CompletedTasks);
        Assert.Equal(0.0, p1.Percentage);

        // 3. Move Task 1 to Done column -> Progress becomes 1 of 2 = 50%
        await boardService.MoveTaskAsync(user.Id, task1.Id, done.Id, 0);

        goals = await goalService.GetUserGoalsAsync(user.Id);
        var p2 = GoalService.CalculateGoalProgress(goals[0]);
        Assert.Equal(2, p2.TotalTasks);
        Assert.Equal(1, p2.CompletedTasks);
        Assert.Equal(50.0, p2.Percentage);

        // 4. Move Task 2 to Done column -> Progress becomes 2 of 2 = 100%
        await boardService.MoveTaskAsync(user.Id, task2.Id, done.Id, 1);

        goals = await goalService.GetUserGoalsAsync(user.Id);
        var p3 = GoalService.CalculateGoalProgress(goals[0]);
        Assert.Equal(2, p3.TotalTasks);
        Assert.Equal(2, p3.CompletedTasks);
        Assert.Equal(100.0, p3.Percentage);
    }

    [Fact]
    public async Task SingleTask_CanBeLinkedToMultipleGoals()
    {
        var user = await CreateTestUserAsync("multigoal");
        var boardService = new BoardService(NewDb);
        var goalService = new GoalService(NewDb);

        var board = (await boardService.CreateBoardAsync(user.Id, "Work")).Data!;
        var task = (await boardService.CreateTaskAsync(user.Id, board.Columns[0].Id, "Publish Article")).Data!;

        var goal1 = (await goalService.CreateGoalAsync(user.Id, "Professional Branding")).Data!;
        var goal2 = (await goalService.CreateGoalAsync(user.Id, "Annual Company Bonus")).Data!;

        await goalService.LinkTaskToGoalAsync(user.Id, task.Id, goal1.Id);
        await goalService.LinkTaskToGoalAsync(user.Id, task.Id, goal2.Id);

        var goals = await goalService.GetUserGoalsAsync(user.Id);
        Assert.Equal(2, goals.Count);
        Assert.Single(goals[0].TaskGoals);
        Assert.Single(goals[1].TaskGoals);
        Assert.Equal(task.Id, goals[0].TaskGoals[0].TaskId);
        Assert.Equal(task.Id, goals[1].TaskGoals[0].TaskId);
    }

    #endregion

    #region C. Local Date Provider & Board Statistics Accuracy

    [Fact]
    public async Task BoardStatistics_DeterministicLocalDates_AndSemantics()
    {
        var fixedDate = new DateOnly(2026, 11, 24);
        var dateProvider = new FixedLocalDateProvider(fixedDate);

        var user = await CreateTestUserAsync("statsdet");
        var service = new BoardService(NewDb, dateProvider);

        var board = (await service.CreateBoardAsync(user.Id, "Sprint 42")).Data!;
        var todo = board.Columns.First(c => c.Name == "To Do");
        var inProg = board.Columns.First(c => c.Name == "In Progress");
        var done = board.Columns.First(c => c.Name == "Done");

        // 1. Task due yesterday (overdue)
        await service.CreateTaskAsync(user.Id, todo.Id, "Overdue 1", dueDate: fixedDate.AddDays(-1));
        // 2. Task due today (due in next 7 days, NOT overdue)
        await service.CreateTaskAsync(user.Id, todo.Id, "Due Today", dueDate: fixedDate);
        // 3. Task due in 6 days (due in next 7 days, inclusive window)
        await service.CreateTaskAsync(user.Id, inProg.Id, "Due in 6 Days", dueDate: fixedDate.AddDays(6));
        // 4. Task due in 7 days (outside 7 days window: today to today+6)
        await service.CreateTaskAsync(user.Id, inProg.Id, "Due in 7 Days", dueDate: fixedDate.AddDays(7));
        // 5. Completed task with past due date -> must NEVER be counted as overdue or due soon!
        await service.CreateTaskAsync(user.Id, done.Id, "Completed Past", dueDate: fixedDate.AddDays(-5));
        // 6. Completed task with future due date
        await service.CreateTaskAsync(user.Id, done.Id, "Completed Future", dueDate: fixedDate.AddDays(2));

        var stats = await service.GetBoardStatisticsAsync(user.Id, board.Id);

        Assert.Equal(6, stats.TotalTasks);
        Assert.Equal(2, stats.InProgressTasks); // tasks 3 and 4 in In Progress column
        Assert.Equal(2, stats.CompletedTasks);  // tasks 5 and 6 in Done column
        Assert.Equal(33.3, stats.CompletionPercentage); // 2/6 * 100 = 33.3%
        Assert.Equal(2, stats.DueInNext7Days); // Task 2 (due today) + Task 3 (due in 6 days)
        Assert.Equal(1, stats.OverdueTasks);   // Task 1 (due yesterday). Task 5 is completed so not counted!
    }

    [Fact]
    public async Task BoardStatistics_EmptyBoard_ReturnsZeroes()
    {
        var user = await CreateTestUserAsync("empty");
        var service = new BoardService(NewDb);
        var board = (await service.CreateBoardAsync(user.Id, "Empty Board")).Data!;

        var stats = await service.GetBoardStatisticsAsync(user.Id, board.Id);

        Assert.Equal(0, stats.TotalTasks);
        Assert.Equal(0, stats.InProgressTasks);
        Assert.Equal(0, stats.CompletedTasks);
        Assert.Equal(0.0, stats.CompletionPercentage);
        Assert.Equal(0, stats.DueInNext7Days);
        Assert.Equal(0, stats.OverdueTasks);
    }

    [Fact]
    public async Task BoardStatistics_CustomAndMultipleCompletedColumns()
    {
        var user = await CreateTestUserAsync("customcols");
        var service = new BoardService(NewDb);

        // Create board with 3 default columns (To Do, In Progress, Done)
        var board = (await service.CreateBoardAsync(user.Id, "Dev Pipeline")).Data!;

        // Add 2 custom columns:
        // "Review" (position 3, non-completed) -> In Progress
        // "Archived" (position 4, completed) -> Completed
        var colReview = (await service.AddColumnAsync(user.Id, board.Id, "Review", isCompleted: false)).Data!;
        var colArchive = (await service.AddColumnAsync(user.Id, board.Id, "Archived", isCompleted: true)).Data!;

        var todo = board.Columns.First(c => c.Name == "To Do");
        var inProg = board.Columns.First(c => c.Name == "In Progress");
        var done = board.Columns.First(c => c.Name == "Done");

        // 1 in To Do (backlog)
        await service.CreateTaskAsync(user.Id, todo.Id, "T1");
        // 1 in In Progress (WIP)
        await service.CreateTaskAsync(user.Id, inProg.Id, "T2");
        // 1 in Review (WIP)
        await service.CreateTaskAsync(user.Id, colReview.Id, "T3");
        // 1 in Done (Completed)
        await service.CreateTaskAsync(user.Id, done.Id, "T4");
        // 1 in Archived (Completed)
        await service.CreateTaskAsync(user.Id, colArchive.Id, "T5");

        var stats = await service.GetBoardStatisticsAsync(user.Id, board.Id);

        Assert.Equal(5, stats.TotalTasks);
        Assert.Equal(2, stats.InProgressTasks); // T2 (In Progress) and T3 (Review)
        Assert.Equal(2, stats.CompletedTasks);  // T4 (Done) and T5 (Archived)
        Assert.Equal(40.0, stats.CompletionPercentage); // 2/5 * 100 = 40.0%
    }

    [Fact]
    public async Task BoardStatistics_IsStrictlyIsolatedPerBoard()
    {
        var user = await CreateTestUserAsync("isostats");
        var service = new BoardService(NewDb);

        var boardA = (await service.CreateBoardAsync(user.Id, "Board A")).Data!;
        var boardB = (await service.CreateBoardAsync(user.Id, "Board B")).Data!;

        await service.CreateTaskAsync(user.Id, boardA.Columns[0].Id, "Task on A");
        await service.CreateTaskAsync(user.Id, boardB.Columns[0].Id, "Task on B 1");
        await service.CreateTaskAsync(user.Id, boardB.Columns[0].Id, "Task on B 2");

        var statsA = await service.GetBoardStatisticsAsync(user.Id, boardA.Id);
        var statsB = await service.GetBoardStatisticsAsync(user.Id, boardB.Id);

        Assert.Equal(1, statsA.TotalTasks);
        Assert.Equal(2, statsB.TotalTasks);
    }

    #endregion
}
