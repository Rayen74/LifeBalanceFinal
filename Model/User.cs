namespace Model;

/// <summary>An account that can sign in to LifeBalance.</summary>
public class User
{
    /// <summary>Generated automatically when the object is created.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Always stored normalized (trimmed + lowercase). Unique in the database.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. The plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set automatically by the DbContext when email or password changes.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>Null until the first successful login.</summary>
    public DateTime? LastAccess { get; set; }
}
