using Controller;
using Microsoft.EntityFrameworkCore;
using Model;
using Xunit;

namespace Tests;

/// <summary>
/// Runs against a REAL PostgreSQL database (a separate one, so your data is safe).
/// Default: lifebalancedb_test on localhost. Override with the LIFEBALANCE_TEST_DB environment variable.
/// The database and the users table are created automatically by the migrations.
/// </summary>
public class AuthServiceTests
{
    private const string Password = "Passw0rdOK";

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("LIFEBALANCE_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=lifebalancedb_test;Username=postgres;Password=12345";

    private static readonly DbContextOptions<LifeBalanceDbContext> Options =
        new DbContextOptionsBuilder<LifeBalanceDbContext>().UseNpgsql(ConnectionString).Options;

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string NewSessionPath() =>
        Path.Combine(Path.GetTempPath(), "lifebalance-tests", Guid.NewGuid().ToString("N"), "session.json");

    private static UserRepository NewRepository() => new(() => new LifeBalanceDbContext(Options));

    /// <summary>A fresh AuthService, like a freshly started application.</summary>
    private static AuthService NewApp(string sessionPath) =>
        new(NewRepository(), new PasswordService(), new SessionManager(sessionPath));

    private static async Task<(AuthService App, string Email, string SessionPath)> RegisteredUserAsync()
    {
        var sessionPath = NewSessionPath();
        var app = NewApp(sessionPath);
        var email = NewEmail();
        var result = await app.RegisterAsync(email, Password, Password);
        Assert.True(result.Success, result.Error);
        return (app, email, sessionPath);
    }

    // 1
    [Fact]
    public async Task NewUserCanRegister()
    {
        var (_, email, _) = await RegisteredUserAsync();

        Assert.NotNull(await NewRepository().FindByEmailAsync(email));
    }

    // 2
    [Fact]
    public async Task GeneratedIdIsAValidUuid()
    {
        var (_, email, _) = await RegisteredUserAsync();
        var user = await NewRepository().FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user!.Id);
        Assert.True(Guid.TryParse(user.Id.ToString(), out _));
    }

    // 3
    [Fact]
    public async Task PasswordIsStoredAsHashNotPlaintext()
    {
        var (_, email, _) = await RegisteredUserAsync();
        var user = await NewRepository().FindByEmailAsync(email);

        Assert.NotEqual(Password, user!.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt format
    }

    // 4
    [Fact]
    public async Task DuplicateEmailIsRejected_EvenWithDifferentCaseAndSpaces()
    {
        var (app, email, _) = await RegisteredUserAsync();

        var result = await app.RegisterAsync("  " + email.ToUpperInvariant() + " ", Password, Password);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task DatabaseItselfRefusesDuplicateEmails()
    {
        var (_, email, _) = await RegisteredUserAsync();

        // Bypass AuthService completely: the unique index must still stop it.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            NewRepository().AddAsync(new User { Email = email, PasswordHash = "x" }));
    }

    // 5
    [Fact]
    public async Task LoginSucceedsWithCorrectCredentials()
    {
        var (app, email, _) = await RegisteredUserAsync();

        var result = await app.LoginAsync(email, Password);

        Assert.True(result.Success, result.Error);
        Assert.True(app.IsAuthenticated);
        Assert.Equal(email, app.CurrentUser!.Email);
    }

    // 6
    [Fact]
    public async Task LoginFailsWithWrongPasswordOrUnknownEmail_WithTheSameGenericMessage()
    {
        var (app, email, _) = await RegisteredUserAsync();

        var wrongPassword = await app.LoginAsync(email, "WrongPassw0rd");
        var unknownEmail = await app.LoginAsync(NewEmail(), Password);

        Assert.False(wrongPassword.Success);
        Assert.False(unknownEmail.Success);
        Assert.Equal(wrongPassword.Error, unknownEmail.Error);
        Assert.False(app.IsAuthenticated);
    }

    // 7
    [Fact]
    public async Task LastAccessIsSetOnLogin_AndLastModifiedIsNotTouched()
    {
        var (app, email, _) = await RegisteredUserAsync();
        var before = (await NewRepository().FindByEmailAsync(email))!;
        Assert.Null(before.LastAccess);

        await app.LoginAsync(email, Password);

        var after = (await NewRepository().FindByEmailAsync(email))!;
        Assert.NotNull(after.LastAccess);
        Assert.Equal(before.LastModified, after.LastModified);
    }

    // 8
    [Fact]
    public async Task LastModifiedChangesWhenAccountInformationChanges()
    {
        var (app, email, _) = await RegisteredUserAsync();
        await app.LoginAsync(email, Password);
        var repo = NewRepository();
        var original = (await repo.FindByEmailAsync(email))!;

        await Task.Delay(50);
        var change = await app.ChangePasswordAsync(Password, "NewPassw0rd2");
        Assert.True(change.Success, change.Error);
        var afterPassword = (await repo.FindByEmailAsync(email))!;
        Assert.True(afterPassword.LastModified > original.LastModified);

        await Task.Delay(50);
        await repo.UpdateEmailAsync(original.Id, NewEmail());
        var afterEmail = (await repo.FindByIdAsync(original.Id))!;
        Assert.True(afterEmail.LastModified > afterPassword.LastModified);
    }

    // 9
    [Fact]
    public async Task SessionSurvivesApplicationRestart()
    {
        var (app, email, sessionPath) = await RegisteredUserAsync();
        await app.LoginAsync(email, Password);

        var restarted = NewApp(sessionPath); // brand-new instance = new process

        Assert.True(restarted.HasSavedSession);
        Assert.True(await restarted.RestoreSessionAsync());
        Assert.True(restarted.IsAuthenticated);
        Assert.Equal(email, restarted.CurrentUser!.Email);
    }

    // 10
    [Fact]
    public async Task WithoutASessionTheUserIsNotAuthenticatedAtStartup()
    {
        var fresh = NewApp(NewSessionPath());

        Assert.False(fresh.HasSavedSession);
        Assert.False(await fresh.RestoreSessionAsync());
        Assert.False(fresh.IsAuthenticated);
    }

    // 11
    [Fact]
    public async Task LogoutClearsTheSession()
    {
        var (app, email, sessionPath) = await RegisteredUserAsync();
        await app.LoginAsync(email, Password);

        app.Logout();

        Assert.False(app.IsAuthenticated);
        Assert.Null(app.CurrentUser);
        Assert.False(app.HasSavedSession);
        Assert.False(await NewApp(sessionPath).RestoreSessionAsync());
    }

    // 12 (the View opens the dashboard only when IsAuthenticated is true, see App.ShowDashboard)
    [Fact]
    public async Task AfterLogoutTheAppIsNoLongerAuthenticated_SoProtectedScreensStayClosed()
    {
        var (app, email, _) = await RegisteredUserAsync();
        await app.LoginAsync(email, Password);
        Assert.True(app.IsAuthenticated);

        app.Logout();

        Assert.False(app.IsAuthenticated);
    }

    [Fact]
    public async Task ChangingThePasswordInvalidatesOlderSavedSessions()
    {
        var (app, email, sessionPath) = await RegisteredUserAsync();
        await app.LoginAsync(email, Password);
        var oldSessionFile = File.ReadAllText(sessionPath);

        await app.ChangePasswordAsync(Password, "NewPassw0rd2");

        File.WriteAllText(sessionPath, oldSessionFile); // an attacker replays the old file
        Assert.False(await NewApp(sessionPath).RestoreSessionAsync());
    }

    // ----- Validation (does not need the database) -----

    [Theory]
    [InlineData("", Password, Password)]
    [InlineData("not-an-email", Password, Password)]
    [InlineData("a@b.com", "short1", "short1")]
    [InlineData("a@b.com", "onlyletters", "onlyletters")]
    [InlineData("a@b.com", "12345678", "12345678")]
    [InlineData("a@b.com", Password, "Different1Pass")]
    public async Task InvalidRegistrationIsRejectedWithAMessage(string email, string password, string confirm)
    {
        var result = await NewApp(NewSessionPath()).RegisterAsync(email, password, confirm);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void EmailNormalizationTrimsAndLowercases() =>
        Assert.Equal("john@example.com", AuthService.NormalizeEmail("  John@Example.COM "));
}
