using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class IdempotencyRepository(PayrollDbContext db) : IIdempotencyRepository
    {
        // A crash leaves a claim behind; after this long a retry may take it over. Keep it longer than your slowest action.
        private static readonly TimeSpan ClaimLasts = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SavedResponseLasts = TimeSpan.FromHours(24);

        public async Task<IdempotencyRecord?> TryClaimAsync(IdempotencyRecord claim)
        {
            var now = DateTime.UtcNow;

            // Removes old responses and abandoned claims on the way in, so no cleanup job is needed.
            await db.IdempotencyRecords.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync();

            // Most repeats are found here, so they don't log a failed insert.
            var existing = await FindAsync(claim.Key);
            if (existing is not null)
                return existing;

            claim.ExpiresAt = now + ClaimLasts;
            db.IdempotencyRecords.Add(claim);
            try
            {
                await db.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException)
            {
                // Lost the race: the primary key lets only one request insert this key, even across servers.
                var winner = await FindAsync(claim.Key);
                if (winner is null)
                    throw;

                return winner;
            }
            finally
            {
                // Otherwise the action's own SaveChanges would try this insert again.
                db.Entry(claim).State = EntityState.Detached;
            }
        }

        public Task CompleteAsync(IdempotencyRecord claim, int statusCode, string? contentType, string? location, byte[] body)
        {
            var expiresAt = DateTime.UtcNow + SavedResponseLasts;

            return Owned(claim).ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.StatusCode, statusCode)
                .SetProperty(r => r.ContentType, contentType)
                .SetProperty(r => r.Location, location)
                .SetProperty(r => r.Body, body)
                .SetProperty(r => r.ExpiresAt, expiresAt));
        }

        public Task ReleaseAsync(IdempotencyRecord claim) => Owned(claim).ExecuteDeleteAsync();

        private Task<IdempotencyRecord?> FindAsync(byte[] key) => db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Key == key);

        // ExpiresAt too: if this claim expired and a retry took it over, that retry's row is left alone.
        private IQueryable<IdempotencyRecord> Owned(IdempotencyRecord claim) => db.IdempotencyRecords.Where(r => r.Key == claim.Key && r.ExpiresAt == claim.ExpiresAt);
    }
}
