using Microsoft.EntityFrameworkCore;
using Model;

namespace Controller;

/// <summary>All database access for users. One short-lived DbContext per call.</summary>
public class UserRepository
{
    private readonly Func<LifeBalanceDbContext> _createContext;

    public UserRepository(Func<LifeBalanceDbContext>? createContext = null)
    {
        _createContext = createContext ?? (() => new LifeBalanceDbContext());
    }

    /// <summary>Creates the database if needed and applies pending migrations.</summary>
    public async Task MigrateAsync()
    {
        await using var db = _createContext();
        await db.Database.MigrateAsync();
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail)
    {
        await using var db = _createContext();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail);
    }

    public async Task<User?> FindByIdAsync(Guid id)
    {
        await using var db = _createContext();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task AddAsync(User user)
    {
        await using var db = _createContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    /// <summary>Sets last_access only. last_modified is intentionally left alone.</summary>
    public async Task<User?> RecordLoginAsync(Guid id)
    {
        await using var db = _createContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        user.LastAccess = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Changes the password hash; the DbContext refreshes last_modified.</summary>
    public async Task<User?> UpdatePasswordHashAsync(Guid id, string newHash)
    {
        await using var db = _createContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        user.PasswordHash = newHash;
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Changes the (already normalized) email; the DbContext refreshes last_modified.</summary>
    public async Task<User?> UpdateEmailAsync(Guid id, string normalizedEmail)
    {
        await using var db = _createContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        user.Email = normalizedEmail;
        await db.SaveChangesAsync();
        return user;
    }
}
