using Domain.Entities;

namespace Application.Interfaces
{
    public interface IIdempotencyRepository
    {
        // Returns null when this request got the claim; otherwise the record that is already there.
        Task<IdempotencyRecord?> TryClaimAsync(IdempotencyRecord claim);

        Task CompleteAsync(IdempotencyRecord claim, int statusCode, string? contentType, string? location, byte[] body);

        Task ReleaseAsync(IdempotencyRecord claim);
    }
}
