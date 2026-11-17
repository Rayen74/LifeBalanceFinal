using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class LifeBalanceDbContext : DbContext
    {
        public LifeBalanceDbContext()
        {
        }

        public LifeBalanceDbContext(DbContextOptions<LifeBalanceDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=lifebalancedb;Username=postgres;Password=12345");
            }
        }
    }
}