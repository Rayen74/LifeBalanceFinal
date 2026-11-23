using Controller;
using Microsoft.EntityFrameworkCore;
using Model;
using Xunit;

namespace Tests;

/// <summary>
/// Unit and integration tests for Productivity module: Kanban boards, tasks, habits, and goals.
/// </summary>
public class ProductivityServiceTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LIFEBALANCE_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=lifebalancedb_test;Username=postgres;Password=12345";

    private static readonly DbContextOptions<LifeBalanceDbContext> Options =
        new DbContextOptionsBuilder<LifeBalanceDbContext>().UseNpgsql(ConnectionString).Options;

    private static LifeBalanceDbContext NewDb() => new(Options);

    private static async Task<User> CreateTestUserAsync(string prefix = "user")
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

    [Fact]
    public async Task CreateBoard_CreatesDefaultColumns_ToDos_InProgress_Done()
    {
        var user = await CreateTestUserAsync("board");
        var service = new BoardService(NewDb);

        var result = await service.CreateBoardAsync(user.Id, "University", "My academic work");
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var details = await service.GetBoardDetailsAsync(user.Id, result.Data!.Id);
        Assert.NotNull(details);
        Assert.Equal(3, details!.Columns.Count);
        Assert.Equal("To Do", details.Columns[0].Name);
        Assert.False(details.Columns[0].IsCompleted);
        Assert.Equal("In Progress", details.Columns[1].Name);
        Assert.False(details.Columns[1].IsCompleted);
        Assert.Equal("Done", details.Columns[2].Name);
        Assert.True(details.Columns[2].IsCompleted);
    }

    [Fact]
    public async Task CreateTask_PersistsAndOrdersByPosition()
    {
        var user = await CreateTestUserAsync("task");
        var service = new BoardService(NewDb);
        var board = (await service.CreateBoardAsync(user.Id, "Work")).Data!;

        var todoCol = board.Columns.First(c => c.Name == "To Do");

        var t1 = await service.CreateTaskAsync(user.Id, todoCol.Id, "Task 1", priority: TaskPriority.High);
        var t2 = await service.CreateTaskAsync(user.Id, todoCol.Id, "Task 2", priority: TaskPriority.Low);

        Assert.True(t1.Success);
        Assert.True(t2.Success);
        Assert.Equal(0, t1.Data!.Position);
        Assert.Equal(1, t2.Data!.Position);
        Assert.Null(t1.Data.CompletedAt);

        var details = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var refreshedTodo = details!.Columns.First(c => c.Id == todoCol.Id);
        Assert.Equal(2, refreshedTodo.Tasks.Count);
        Assert.Equal("Task 1", refreshedTodo.Tasks[0].Title);
        Assert.Equal("Task 2", refreshedTodo.Tasks[1].Title);
    }

    [Fact]
    public async Task MoveTask_IntoDone_SetsCompletedAt_AndOutOfDone_ClearsIt()
    {
        var user = await CreateTestUserAsync("move");
        var service = new BoardService(NewDb);
        var board = (await service.CreateBoardAsync(user.Id, "Sprint")).Data!;

        var todoCol = board.Columns.First(c => c.Name == "To Do");
        var doneCol = board.Columns.First(c => c.Name == "Done");

        var task = (await service.CreateTaskAsync(user.Id, todoCol.Id, "Feature X")).Data!;
        Assert.Null(task.CompletedAt);

        // Move to Done
        var move1 = await service.MoveTaskAsync(user.Id, task.Id, doneCol.Id, 0);
        Assert.True(move1.Success);

        var details1 = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var taskInDone = details1!.Columns.First(c => c.Id == doneCol.Id).Tasks.First();
        Assert.NotNull(taskInDone.CompletedAt);

        // Move back to To Do
        var move2 = await service.MoveTaskAsync(user.Id, task.Id, todoCol.Id, 0);
        Assert.True(move2.Success);

        var details2 = await service.GetBoardDetailsAsync(user.Id, board.Id);
        var taskInTodo = details2!.Columns.First(c => c.Id == todoCol.Id).Tasks.First();
        Assert.Null(taskInTodo.CompletedAt);
    }

    [Fact]
    public async Task UserCannotAccessOrModifyAnotherUsersBoardOrTask()
    {
        var userA = await CreateTestUserAsync("userA");
        var userB = await CreateTestUserAsync("userB");
        var service = new BoardService(NewDb);

        var boardA = (await service.CreateBoardAsync(userA.Id, "Secret A")).Data!;
        var colA = boardA.Columns.First();
        var taskA = (await service.CreateTaskAsync(userA.Id, colA.Id, "Task secret")).Data!;

        // User B cannot get board A
        var readB = await service.GetBoardDetailsAsync(userB.Id, boardA.Id);
        Assert.Null(readB);

        // User B cannot delete board A
        var deleteBoardB = await service.DeleteBoardAsync(userB.Id, boardA.Id);
        Assert.False(deleteBoardB.Success);

        // User B cannot create task in column of Board A
        var createTaskB = await service.CreateTaskAsync(userB.Id, colA.Id, "Injected task");
        Assert.False(createTaskB.Success);

        // User B cannot delete task of User A
        var deleteTaskB = await service.DeleteTaskAsync(userB.Id, taskA.Id);
        Assert.False(deleteTaskB.Success);
    }

    [Fact]
    public async Task BoardStatistics_CalculatesAccurately()
    {
        var user = await CreateTestUserAsync("stats");
        var service = new BoardService(NewDb);
        var board = (await service.CreateBoardAsync(user.Id, "Stats Board")).Data!;

        var todo = board.Columns.First(c => c.Name == "To Do");
        var inProg = board.Columns.First(c => c.Name == "In Progress");
        var done = board.Columns.First(c => c.Name == "Done");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1 To Do overdue
        await service.CreateTaskAsync(user.Id, todo.Id, "Overdue task", dueDate: today.AddDays(-2));
        // 1 In Progress due in 3 days
        await service.CreateTaskAsync(user.Id, inProg.Id, "Mid task", dueDate: today.AddDays(3));
        // 1 Done
        await service.CreateTaskAsync(user.Id, done.Id, "Completed task");

        var stats = await service.GetBoardStatisticsAsync(user.Id, board.Id);

        Assert.Equal(3, stats.TotalTasks);
        Assert.Equal(1, stats.InProgressTasks);
        Assert.Equal(1, stats.CompletedTasks);
        Assert.Equal(33.3, stats.CompletionPercentage);
        Assert.Equal(1, stats.DueInNext7Days);
        Assert.Equal(1, stats.OverdueTasks);
    }

    [Fact]
    public async Task PersonalGoal_And_TaskGoal_LinkAndIsolation()
    {
        var userA = await CreateTestUserAsync("goalA");
        var userB = await CreateTestUserAsync("goalB");
        var boardService = new BoardService(NewDb);
        var goalService = new GoalService(NewDb);

        var boardA = (await boardService.CreateBoardAsync(userA.Id, "Life")).Data!;
        var taskA = (await boardService.CreateTaskAsync(userA.Id, boardA.Columns[0].Id, "Study")).Data!;

        var goalA = (await goalService.CreateGoalAsync(userA.Id, "Graduate with Honors")).Data!;
        var goalB = (await goalService.CreateGoalAsync(userB.Id, "User B Goal")).Data!;

        // Link valid task and goal for User A
        var linkResult = await goalService.LinkTaskToGoalAsync(userA.Id, taskA.Id, goalA.Id);
        Assert.True(linkResult.Success);

        // Duplicate link is rejected
        var duplicateLink = await goalService.LinkTaskToGoalAsync(userA.Id, taskA.Id, goalA.Id);
        Assert.False(duplicateLink.Success);

        // Cross-user linking is rejected
        var crossLink = await goalService.LinkTaskToGoalAsync(userA.Id, taskA.Id, goalB.Id);
        Assert.False(crossLink.Success);

        // Read goals
        var goals = await goalService.GetUserGoalsAsync(userA.Id);
        Assert.Single(goals);
        Assert.Single(goals[0].TaskGoals);
        Assert.Equal("Study", goals[0].TaskGoals[0].Task!.Title);
    }

    [Fact]
    public async Task HabitService_RecordsCompletions_PreventsDuplicate_AndCalculatesStreak()
    {
        var user = await CreateTestUserAsync("habit");
        var habitService = new HabitService(NewDb);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var habit = (await habitService.CreateHabitAsync(
            user.Id,
            "Morning Run",
            "5km",
            HabitFrequency.Daily,
            startDate: today.AddDays(-10))).Data!;

        // Cannot complete before start date
        var earlyRes = await habitService.RecordCompletionAsync(user.Id, habit.Id, today.AddDays(-15));
        Assert.False(earlyRes.Success);

        // Record today, yesterday, and day before yesterday (3 days streak)
        await habitService.RecordCompletionAsync(user.Id, habit.Id, today);
        await habitService.RecordCompletionAsync(user.Id, habit.Id, today.AddDays(-1));
        await habitService.RecordCompletionAsync(user.Id, habit.Id, today.AddDays(-2));

        // Duplicate click on today is harmless
        var dupRes = await habitService.RecordCompletionAsync(user.Id, habit.Id, today);
        Assert.True(dupRes.Success);

        int streak = await habitService.CalculateCurrentStreakAsync(user.Id, habit.Id, today);
        Assert.Equal(3, streak);

        // Remove yesterday completion -> breaks streak
        await habitService.RemoveCompletionAsync(user.Id, habit.Id, today.AddDays(-1));
        int brokenStreak = await habitService.CalculateCurrentStreakAsync(user.Id, habit.Id, today);
        Assert.Equal(1, brokenStreak); // Only today remains
    }

    [Fact]
    public void CalculateStreakFromDates_PureUnitLogic()
    {
        var today = new DateOnly(2026, 11, 20);

        // Case 1: Today, yesterday, 2 days ago = streak 3
        var dates1 = new List<DateOnly> { today, today.AddDays(-1), today.AddDays(-2), today.AddDays(-5) };
        Assert.Equal(3, HabitService.CalculateStreakFromDates(dates1, today));

        // Case 2: Yesterday, 2 days ago (today not yet completed) = streak 2
        var dates2 = new List<DateOnly> { today.AddDays(-1), today.AddDays(-2) };
        Assert.Equal(2, HabitService.CalculateStreakFromDates(dates2, today));

        // Case 3: Empty = streak 0
        Assert.Equal(0, HabitService.CalculateStreakFromDates(new List<DateOnly>(), today));
    }
}
