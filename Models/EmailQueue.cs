using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentControlSystem.Auth.API.Models
{
    public class EmailQueue
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [EmailAddress]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsHtml { get; set; } = true;

        public bool IsSent { get; set; } = false;

        public int RetryCount { get; set; } = 0;

        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public DateTime? LastAttempt { get; set; }
    }
}