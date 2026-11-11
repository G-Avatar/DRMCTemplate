using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Context
{
    public class PayrollDbContext(DbContextOptions<PayrollDbContext> options) : DbContext(options)
    {
        public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

        public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuthSession>().HasIndex(s => s.ExpiresAt);

            modelBuilder.Entity<IdempotencyRecord>(record =>
            {
                record.HasIndex(r => r.ExpiresAt);
                record.Property(r => r.Key).HasMaxLength(32);
                record.Property(r => r.RequestHash).HasMaxLength(32);
            });
        }
    }
}
