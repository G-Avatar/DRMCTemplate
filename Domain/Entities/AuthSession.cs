using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    // One signed-in session. The cookie only holds Id, so deleting this row ends the session even if someone copied the cookie.
    public class AuthSession
    {
        [Key]
        public Guid Id { get; set; }

        public required byte[] Ticket { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
