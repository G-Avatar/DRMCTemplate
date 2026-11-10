using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Context
{
    public class PayrollDbContext(DbContextOptions<PayrollDbContext> options) : DbContext(options)
    {
        public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<AuthSession>().HasIndex(s => s.ExpiresAt);
    }
}
