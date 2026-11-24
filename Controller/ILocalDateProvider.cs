namespace Controller;

/// <summary>
/// Abstraction for retrieving the current local calendar date.
/// Ensures desktop user experience aligns with their local calendar day rather than UTC day shifts.
/// </summary>
public interface ILocalDateProvider
{
    DateOnly GetToday();
}

/// <summary>
/// Production date provider based on the local machine clock.
/// </summary>
public class SystemLocalDateProvider : ILocalDateProvider
{
    public static readonly SystemLocalDateProvider Instance = new();
    public DateOnly GetToday() => DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>
/// Deterministic date provider for unit and integration testing.
/// </summary>
public class FixedLocalDateProvider : ILocalDateProvider
{
    public DateOnly Today { get; set; }

    public FixedLocalDateProvider(DateOnly today)
    {
        Today = today;
    }

    public DateOnly GetToday() => Today;
}
