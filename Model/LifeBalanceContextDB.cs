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
        public DbSet<TaskBoard> TaskBoards => Set<TaskBoard>();
        public DbSet<TaskColumn> TaskColumns => Set<TaskColumn>();
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<PersonalGoal> PersonalGoals => Set<PersonalGoal>();
        public DbSet<TaskGoal> TaskGoals => Set<TaskGoal>();
        public DbSet<Habit> Habits => Set<Habit>();
        public DbSet<HabitCompletion> HabitCompletions => Set<HabitCompletion>();

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
            // User configuration
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

                entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");
            });

            // TaskBoard configuration
            modelBuilder.Entity<TaskBoard>(entity =>
            {
                entity.ToTable("task_boards");
                entity.HasKey(b => b.Id);

                entity.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(b => b.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(b => b.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(b => b.Description).HasColumnName("description").HasMaxLength(500);
                entity.Property(b => b.CreatedAt).HasColumnName("created_at");

                entity.HasOne(b => b.User)
                      .WithMany()
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(b => b.UserId).HasDatabaseName("ix_task_boards_user_id");
            });

            // TaskColumn configuration
            modelBuilder.Entity<TaskColumn>(entity =>
            {
                entity.ToTable("task_columns");
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(c => c.BoardId).HasColumnName("board_id").IsRequired();
                entity.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(c => c.Position).HasColumnName("position").IsRequired();
                entity.Property(c => c.IsCompleted).HasColumnName("is_completed").IsRequired();

                entity.HasOne(c => c.Board)
                      .WithMany(b => b.Columns)
                      .HasForeignKey(c => c.BoardId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(c => new { c.BoardId, c.Position }).HasDatabaseName("ix_task_columns_board_position");
            });

            // TaskItem configuration
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("task_items");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(t => t.ColumnId).HasColumnName("column_id").IsRequired();
                entity.Property(t => t.CategoryId).HasColumnName("category_id");
                entity.Property(t => t.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
                entity.Property(t => t.Description).HasColumnName("description").HasMaxLength(2000);
                entity.Property(t => t.Priority).HasColumnName("priority").HasConversion<int>().IsRequired();
                entity.Property(t => t.DueDate).HasColumnName("due_date");
                entity.Property(t => t.EstimatedDuration).HasColumnName("estimated_duration");
                entity.Property(t => t.Position).HasColumnName("position").IsRequired();
                entity.Property(t => t.CreatedAt).HasColumnName("created_at");
                entity.Property(t => t.CompletedAt).HasColumnName("completed_at");

                entity.HasOne(t => t.Column)
                      .WithMany(c => c.Tasks)
                      .HasForeignKey(t => t.ColumnId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => new { t.ColumnId, t.Position }).HasDatabaseName("ix_task_items_column_position");
            });

            // PersonalGoal configuration
            modelBuilder.Entity<PersonalGoal>(entity =>
            {
                entity.ToTable("personal_goals");
                entity.HasKey(g => g.Id);

                entity.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(g => g.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(g => g.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
                entity.Property(g => g.Description).HasColumnName("description").HasMaxLength(1000);
                entity.Property(g => g.TargetDate).HasColumnName("target_date");
                entity.Property(g => g.Status).HasColumnName("status").HasConversion<int>().IsRequired();
                entity.Property(g => g.CreatedAt).HasColumnName("created_at");

                entity.HasOne(g => g.User)
                      .WithMany()
                      .HasForeignKey(g => g.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(g => g.UserId).HasDatabaseName("ix_personal_goals_user_id");
            });

            // TaskGoal configuration (many-to-many join)
            modelBuilder.Entity<TaskGoal>(entity =>
            {
                entity.ToTable("task_goals");
                entity.HasKey(tg => new { tg.TaskId, tg.GoalId });

                entity.Property(tg => tg.TaskId).HasColumnName("task_id");
                entity.Property(tg => tg.GoalId).HasColumnName("goal_id");

                entity.HasOne(tg => tg.Task)
                      .WithMany(t => t.TaskGoals)
                      .HasForeignKey(tg => tg.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tg => tg.Goal)
                      .WithMany(g => g.TaskGoals)
                      .HasForeignKey(tg => tg.GoalId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Habit configuration
            modelBuilder.Entity<Habit>(entity =>
            {
                entity.ToTable("habits");
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(h => h.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(h => h.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(h => h.Description).HasColumnName("description").HasMaxLength(500);
                entity.Property(h => h.Frequency).HasColumnName("frequency").HasConversion<int>().IsRequired();
                entity.Property(h => h.StartDate).HasColumnName("start_date").IsRequired();
                entity.Property(h => h.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(h => h.CreatedAt).HasColumnName("created_at");

                entity.HasOne(h => h.User)
                      .WithMany()
                      .HasForeignKey(h => h.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(h => h.UserId).HasDatabaseName("ix_habits_user_id");
            });

            // HabitCompletion configuration
            modelBuilder.Entity<HabitCompletion>(entity =>
            {
                entity.ToTable("habit_completions");
                entity.HasKey(hc => hc.Id);

                entity.Property(hc => hc.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(hc => hc.HabitId).HasColumnName("habit_id").IsRequired();
                entity.Property(hc => hc.CompletedOn).HasColumnName("completed_on").IsRequired();

                entity.HasOne(hc => hc.Habit)
                      .WithMany(h => h.Completions)
                      .HasForeignKey(hc => hc.HabitId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Enforce unique (habit_id, completed_on)
                entity.HasIndex(hc => new { hc.HabitId, hc.CompletedOn })
                      .IsUnique()
                      .HasDatabaseName("ix_habit_completions_habit_date");
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
