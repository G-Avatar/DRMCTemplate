using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    // One request made to an [Idempotent] action. StatusCode stays null while the action is still running.
    public class IdempotencyRecord
    {
        [Key]
        public required byte[] Key { get; set; }

        public required byte[] RequestHash { get; set; }

        public int? StatusCode { get; set; }

        public string? ContentType { get; set; }

        public string? Location { get; set; }

        public byte[]? Body { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
