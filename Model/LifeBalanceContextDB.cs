using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class LifeBalanceDbContext : DbContext
    {
        // Same default as before. Set the LIFEBALANCE_DB environment variable to use another connection string.
        private const string DefaultConnection =
            "Host=localhost;Port=5432;Database=lifebalancedb;Username=postgres;Password=12345";

        public LifeBalanceDbContext()
        {
        }

        public LifeBalanceDbContext(DbContextOptions<LifeBalanceDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connection = Environment.GetEnvironmentVariable("LIFEBALANCE_DB");
                optionsBuilder.UseNpgsql(string.IsNullOrWhiteSpace(connection) ? DefaultConnection : connection);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.Id);

                entity.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
                entity.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
                entity.Property(u => u.CreatedAt).HasColumnName("created_at");
                entity.Property(u => u.LastModified).HasColumnName("last_modified");
                entity.Property(u => u.LastAccess).HasColumnName("last_access");

                // Uniqueness is enforced by PostgreSQL itself, not only in C#.
                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            TouchLastModified();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            TouchLastModified();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Refreshes last_modified when account information (email / password) changed.
        /// Updating only last_access (a login) does not count as a modification.
        /// </summary>
        private void TouchLastModified()
        {
            foreach (var entry in ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Modified))
            {
                var accountChanged = entry.Properties.Any(p =>
                    p.IsModified &&
                    p.Metadata.Name != nameof(User.LastAccess) &&
                    p.Metadata.Name != nameof(User.LastModified));

                if (accountChanged)
                    entry.Entity.LastModified = DateTime.UtcNow;
            }
        }
    }
}
