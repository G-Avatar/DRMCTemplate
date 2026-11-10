using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace WebApi.Extensions
{
    // Cookie auth calls this to keep each session and its tokens in the database, so the cookie is only a pointer and logging out ends a copied cookie too.
    // A singleton, because the cookie options hold one instance; the repository is scoped, so each call opens its own scope.
    public class SessionTicketStore(IServiceScopeFactory scopes, IDataProtectionProvider dataProtection) : ITicketStore
    {
        // The rows hold tokens, so someone able to read the table must not be able to use them.
        private readonly IDataProtector _protector = dataProtection.CreateProtector(nameof(SessionTicketStore));

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var session = ToSession(Guid.NewGuid(), ticket);
            await using var scope = scopes.CreateAsyncScope();
            await Sessions(scope).AddAsync(session);
            return session.Id.ToString();
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            await using var scope = scopes.CreateAsyncScope();
            await Sessions(scope).UpdateAsync(ToSession(Guid.Parse(key), ticket));
        }

        public async Task<AuthenticationTicket?> RetrieveAsync(string key)
        {
            await using var scope = scopes.CreateAsyncScope();
            var session = await Sessions(scope).FindAsync(Guid.Parse(key));
            return session is null ? null : TicketSerializer.Default.Deserialize(_protector.Unprotect(session.Ticket));
        }

        public async Task RemoveAsync(string key)
        {
            await using var scope = scopes.CreateAsyncScope();
            await Sessions(scope).RemoveAsync(Guid.Parse(key));
        }

        private static IAuthSessionRepository Sessions(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IAuthSessionRepository>();

        // The cookie handler always sets ExpiresUtc before it calls the store.
        private AuthSession ToSession(Guid id, AuthenticationTicket ticket) => new()
        {
            Id = id,
            Ticket = _protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            ExpiresAt = ticket.Properties.ExpiresUtc!.Value.UtcDateTime,
        };
    }
}
