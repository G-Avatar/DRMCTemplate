using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Tests
{
    // A throwaway in-memory SQLite, never your real database.
    public sealed class UnitOfWorkTests : IAsyncLifetime
    {
        private readonly string _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private SqliteConnection _keepAlive = null!;

        public async Task InitializeAsync()
        {
            _keepAlive = new SqliteConnection(_connectionString);
            await _keepAlive.OpenAsync();
            await using var db = NewDb();
            await db.Database.EnsureCreatedAsync();
        }

        public Task DisposeAsync() => _keepAlive.DisposeAsync().AsTask();

        [Fact]
        public async Task CommitsEveryWriteInside()
        {
            await using (var db = NewDb())
                await new UnitOfWork(db).InTransactionAsync(async () =>
                {
                    db.AuthSessions.Add(Session());
                    await db.SaveChangesAsync();
                    db.AuthSessions.Add(Session());
                    return await db.SaveChangesAsync();
                });

            await using var check = NewDb();
            Assert.Equal(2, await check.AuthSessions.CountAsync());
        }

        // The nested call joins the outer transaction, so the outer failure undoes its already-saved write too.
        [Fact]
        public async Task AFailureUndoesEveryWriteInsideIncludingNestedOnes()
        {
            await using (var db = NewDb())
            {
                var uow = new UnitOfWork(db);
                await Assert.ThrowsAsync<InvalidOperationException>(() => uow.InTransactionAsync<int>(async () =>
                {
                    await uow.InTransactionAsync(async () =>
                    {
                        db.AuthSessions.Add(Session());
                        return await db.SaveChangesAsync();
                    });
                    throw new InvalidOperationException("boom");
                }));
            }

            await using var check = NewDb();
            Assert.Equal(0, await check.AuthSessions.CountAsync());
        }

        private static AuthSession Session() => new() { Id = Guid.NewGuid(), Ticket = [1], ExpiresAt = DateTime.UtcNow };

        private PayrollDbContext NewDb() => new(new DbContextOptionsBuilder<PayrollDbContext>().UseSqlite(_connectionString).Options);
    }
}
