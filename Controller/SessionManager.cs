namespace Controller;

/// <summary>
/// Remembers who is signed in between application runs, using a small file in the user's
/// own profile folder (e.g. %AppData%\LifeBalance\session.json on Windows).
/// It stores a user id and a token; AuthService decides whether they are still valid.
/// </summary>
public class SessionManager
{
    private readonly string _path;

    public SessionManager(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LifeBalance", "session.json");
    }

    public void Save(Guid userId, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, userId + Environment.NewLine + token);

        // On Linux/macOS make the file readable by the owner only.
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Returns the saved session, or null if there is none or it is unreadable.</summary>
    public (Guid UserId, string Token)? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;

            var lines = File.ReadAllLines(_path);
            if (lines.Length < 2 || !Guid.TryParse(lines[0], out var id) || string.IsNullOrWhiteSpace(lines[1]))
                return null;

            return (id, lines[1].Trim());
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch
        {
            // Nothing else we can do; the in-memory session is already cleared.
        }
    }
}
