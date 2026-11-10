using Domain.Entities;

namespace Application.Interfaces
{
    public interface IAuthSessionRepository
    {
        Task AddAsync(AuthSession session);

        Task UpdateAsync(AuthSession session);

        Task<AuthSession?> FindAsync(Guid id);

        Task RemoveAsync(Guid id);
    }
}
