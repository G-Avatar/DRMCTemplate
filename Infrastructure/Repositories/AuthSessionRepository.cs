using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AuthSessionRepository(PayrollDbContext db) : IAuthSessionRepository
    {
        public async Task AddAsync(AuthSession session)
        {
            var now = DateTime.UtcNow;

            // Removes sessions nobody came back to on the way in, so no cleanup job is needed.
            await db.AuthSessions.Where(s => s.ExpiresAt <= now).ExecuteDeleteAsync();

            db.AuthSessions.Add(session);
            await db.SaveChangesAsync();
        }

        public Task UpdateAsync(AuthSession session) => db.AuthSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(s => s.Ticket, session.Ticket)
            .SetProperty(s => s.ExpiresAt, session.ExpiresAt));

        public Task<AuthSession?> FindAsync(Guid id) => db.AuthSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);

        public Task RemoveAsync(Guid id) => db.AuthSessions.Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}
