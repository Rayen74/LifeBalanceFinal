using Microsoft.EntityFrameworkCore;
using Model;

namespace Controller;

/// <summary>
/// Service managing habits, daily completion history, and streak calculations.
/// </summary>
public class HabitService
{
    private readonly Func<LifeBalanceDbContext> _dbFactory;
    private readonly ILocalDateProvider _dateProvider;

    public HabitService(Func<LifeBalanceDbContext>? dbFactory = null, ILocalDateProvider? dateProvider = null)
    {
        _dbFactory = dbFactory ?? (() => new LifeBalanceDbContext());
        _dateProvider = dateProvider ?? SystemLocalDateProvider.Instance;
    }

    /// <summary>
    /// Lists all habits for a user, optionally including completions for a given date range.
    /// </summary>
    public async Task<List<Habit>> GetUserHabitsAsync(Guid userId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        await using var db = _dbFactory();
        var query = db.Habits
            .AsNoTracking()
            .Where(h => h.UserId == userId);

        if (fromDate.HasValue && toDate.HasValue)
        {
            query = query.Include(h => h.Completions
                .Where(c => c.CompletedOn >= fromDate.Value && c.CompletedOn <= toDate.Value));
        }
        else
        {
            query = query.Include(h => h.Completions);
        }

        return await query
            .OrderBy(h => !h.IsActive)
            .ThenBy(h => h.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new habit.
    /// </summary>
    public async Task<OperationResult<Habit>> CreateHabitAsync(
        Guid userId,
        string name,
        string? description,
        HabitFrequency frequency,
        DateOnly startDate)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<Habit>.Fail("Habit name cannot be empty.");

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Frequency = frequency,
            StartDate = startDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = _dbFactory();
        db.Habits.Add(habit);
        await db.SaveChangesAsync();

        return OperationResult<Habit>.Ok(habit);
    }

    /// <summary>
    /// Updates habit properties.
    /// </summary>
    public async Task<OperationResult> UpdateHabitAsync(
        Guid userId,
        Guid habitId,
        string name,
        string? description,
        HabitFrequency frequency,
        DateOnly startDate,
        bool isActive)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Habit name cannot be empty.");

        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return OperationResult.Fail("Habit not found or access denied.");

        habit.Name = name;
        habit.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        habit.Frequency = frequency;
        habit.StartDate = startDate;
        habit.IsActive = isActive;

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Sets habit active or inactive.
    /// </summary>
    public async Task<OperationResult> SetHabitActiveAsync(Guid userId, Guid habitId, bool isActive)
    {
        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return OperationResult.Fail("Habit not found or access denied.");

        habit.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Deletes a habit and its completion history.
    /// </summary>
    public async Task<OperationResult> DeleteHabitAsync(Guid userId, Guid habitId)
    {
        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return OperationResult.Fail("Habit not found or access denied.");

        db.Habits.Remove(habit);
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    /// <summary>
    /// Records a habit completion on a given date.
    /// Prevents duplicate completions and completions before StartDate or on inactive habits.
    /// </summary>
    public async Task<OperationResult> RecordCompletionAsync(Guid userId, Guid habitId, DateOnly completedOn)
    {
        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return OperationResult.Fail("Habit not found or access denied.");

        if (!habit.IsActive)
            return OperationResult.Fail("Cannot record completions for an inactive habit.");

        if (completedOn < habit.StartDate)
            return OperationResult.Fail("Completion date cannot precede the habit start date.");

        // Check if already completed on that date
        bool alreadyDone = await db.HabitCompletions
            .AnyAsync(hc => hc.HabitId == habitId && hc.CompletedOn == completedOn);

        if (alreadyDone)
            return OperationResult.Ok(); // Harmless duplicate click

        var completion = new HabitCompletion
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            CompletedOn = completedOn
        };

        db.HabitCompletions.Add(completion);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique constraint ix_habit_completions_habit_date caught it
            return OperationResult.Ok();
        }

        return OperationResult.Ok();
    }

    /// <summary>
    /// Removes a recorded completion for a habit on a given date.
    /// </summary>
    public async Task<OperationResult> RemoveCompletionAsync(Guid userId, Guid habitId, DateOnly completedOn)
    {
        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return OperationResult.Fail("Habit not found or access denied.");

        var completion = await db.HabitCompletions
            .FirstOrDefaultAsync(hc => hc.HabitId == habitId && hc.CompletedOn == completedOn);

        if (completion != null)
        {
            db.HabitCompletions.Remove(completion);
            await db.SaveChangesAsync();
        }

        return OperationResult.Ok();
    }

    /// <summary>
    /// Calculates the current streak (consecutive days ending on today or yesterday) from completion records.
    /// </summary>
    public async Task<int> CalculateCurrentStreakAsync(Guid userId, Guid habitId, DateOnly? referenceDate = null)
    {
        await using var db = _dbFactory();
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit == null)
            return 0;

        var dates = await db.HabitCompletions
            .Where(hc => hc.HabitId == habitId)
            .Select(hc => hc.CompletedOn)
            .OrderByDescending(d => d)
            .ToListAsync();

        return CalculateStreakFromDates(dates, referenceDate ?? _dateProvider.GetToday());
    }

    /// <summary>
    /// Pure calculation method for streak from ordered dates (descending).
    /// </summary>
    public static int CalculateStreakFromDates(IEnumerable<DateOnly> completionDates, DateOnly today)
    {
        var set = new HashSet<DateOnly>(completionDates);
        if (set.Count == 0)
            return 0;

        int streak = 0;
        var checkDate = today;

        // If today is not completed yet, streak might continue from yesterday
        if (!set.Contains(checkDate))
        {
            checkDate = today.AddDays(-1);
        }

        while (set.Contains(checkDate))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
        }

        return streak;
    }
}
