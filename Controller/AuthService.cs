using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Model;

namespace Controller;

/// <summary>Outcome of an authentication action. The View only shows <see cref="Error"/>.</summary>
public record AuthResult(bool Success, string? Error = null)
{
    public static AuthResult Ok() => new(true);
    public static AuthResult Fail(string error) => new(false, error);
}

/// <summary>Register, log in, log out and restore the saved session. Screens call this and nothing else.</summary>
public class AuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string DatabaseUnavailable =
        "Could not reach the database. Check that PostgreSQL is running and try again.";

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly UserRepository _users;
    private readonly PasswordService _passwords;
    private readonly SessionManager _session;
    private bool _databaseReady;

    // Used to spend the same time on "unknown email" as on "wrong password".
    private readonly Lazy<string> _dummyHash;

    public AuthService(UserRepository? users = null, PasswordService? passwords = null,
        SessionManager? session = null)
    {
        _users = users ?? new UserRepository();
        _passwords = passwords ?? new PasswordService();
        _session = session ?? new SessionManager();
        _dummyHash = new Lazy<string>(() => _passwords.Hash("not-a-real-password"));
    }

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    /// <summary>Trim + lowercase. Used everywhere an email is stored or compared.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // ---------- Startup ----------

    /// <summary>True when a session file exists (it may still turn out to be invalid).</summary>
    public bool HasSavedSession => _session.Load() != null;

    /// <summary>Creates/updates the database schema once per run, the first time the database is needed.</summary>
    private async Task EnsureDatabaseAsync()
    {
        if (_databaseReady) return;
        await _users.MigrateAsync();
        _databaseReady = true;
    }

    /// <summary>Signs the user in again from the saved session file, if it is still valid.</summary>
    public async Task<bool> RestoreSessionAsync()
    {
        var saved = _session.Load();
        if (saved == null) return false;

        try
        {
            await EnsureDatabaseAsync();
            var user = await _users.FindByIdAsync(saved.Value.UserId);
            if (user == null || !TokensMatch(saved.Value.Token, CreateSessionToken(user)))
            {
                _session.Clear(); // deleted account, changed password or tampered file
                return false;
            }

            CurrentUser = user;
            return true;
        }
        catch
        {
            return false; // database unreachable: show Login, but keep the saved session for next time
        }
    }

    // ---------- Registration ----------

    public async Task<AuthResult> RegisterAsync(string? email, string? password, string? confirmPassword)
    {
        var normalized = NormalizeEmail(email);

        if (normalized.Length == 0 || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            return AuthResult.Fail("Please fill in email, password and confirm password.");
        if (!IsValidEmail(normalized))
            return AuthResult.Fail("Please enter a valid email address.");
        var passwordError = ValidatePassword(password);
        if (passwordError != null)
            return AuthResult.Fail(passwordError);
        if (password != confirmPassword)
            return AuthResult.Fail("Passwords do not match.");

        try
        {
            await EnsureDatabaseAsync();
            if (await _users.FindByEmailAsync(normalized) != null)
                return AuthResult.Fail("An account with this email already exists.");

            await _users.AddAsync(new User
            {
                Email = normalized,
                PasswordHash = _passwords.Hash(password)
            });
            return AuthResult.Ok();
        }
        catch (DbUpdateException)
        {
            // Two registrations at the same moment: the unique index in PostgreSQL stopped the second one.
            return AuthResult.Fail("An account with this email already exists.");
        }
        catch
        {
            return AuthResult.Fail(DatabaseUnavailable);
        }
    }

    // ---------- Login / Logout ----------

    public async Task<AuthResult> LoginAsync(string? email, string? password)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0 || string.IsNullOrEmpty(password))
            return AuthResult.Fail("Please enter your email and password.");

        try
        {
            await EnsureDatabaseAsync();
            var user = await _users.FindByEmailAsync(normalized);

            // Always run a verification so a wrong email and a wrong password look (and feel) identical.
            var passwordOk = _passwords.Verify(password, user?.PasswordHash ?? _dummyHash.Value);
            if (user == null || !passwordOk)
                return AuthResult.Fail(InvalidCredentials);

            var updated = await _users.RecordLoginAsync(user.Id);
            if (updated == null)
                return AuthResult.Fail(InvalidCredentials);

            CurrentUser = updated;
            _session.Save(updated.Id, CreateSessionToken(updated));
            return AuthResult.Ok();
        }
        catch
        {
            return AuthResult.Fail(DatabaseUnavailable);
        }
    }

    public void Logout()
    {
        CurrentUser = null;
        _session.Clear();
    }

    // ---------- Account changes ----------

    public async Task<AuthResult> ChangePasswordAsync(string? currentPassword, string? newPassword)
    {
        if (CurrentUser == null)
            return AuthResult.Fail("You are not signed in.");
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            return AuthResult.Fail("Please fill in both passwords.");
        if (!_passwords.Verify(currentPassword, CurrentUser.PasswordHash))
            return AuthResult.Fail("Current password is incorrect.");
        var passwordError = ValidatePassword(newPassword);
        if (passwordError != null)
            return AuthResult.Fail(passwordError);

        try
        {
            var updated = await _users.UpdatePasswordHashAsync(CurrentUser.Id, _passwords.Hash(newPassword));
            if (updated == null)
                return AuthResult.Fail("Account not found.");

            CurrentUser = updated;
            _session.Save(updated.Id, CreateSessionToken(updated)); // the old token stops working
            return AuthResult.Ok();
        }
        catch
        {
            return AuthResult.Fail(DatabaseUnavailable);
        }
    }

    // ---------- Validation helpers ----------

    private static bool IsValidEmail(string normalizedEmail) =>
        normalizedEmail.Length <= 254 && EmailPattern.IsMatch(normalizedEmail);

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    private static string? ValidatePassword(string password)
    {
        if (password.Length < 8)
            return "Password must be at least 8 characters.";
        // BCrypt only reads the first 72 bytes, so longer passwords are refused instead of silently cut.
        if (Encoding.UTF8.GetByteCount(password) > 72)
            return "Password is too long (72 bytes maximum).";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must contain at least one letter and one number.";
        return null;
    }

    // ---------- Session token ----------

    /// <summary>
    /// A token only the database can reproduce: HMAC(user id) keyed with the user's password hash.
    /// Changing the password changes the hash, so every older saved session becomes invalid.
    /// </summary>
    private static string CreateSessionToken(User user)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(user.PasswordHash));
        return Convert.ToBase64String(hmac.ComputeHash(user.Id.ToByteArray()));
    }

    private static bool TokensMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
