using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookKeepingWeb.Models
{
    public class ForumComment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(2000, ErrorMessage = "Comment must be under 2000 characters.")]
        public required string Content { get; set; }

        [Required]
        public required string CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual required UserProfile CreatedByUser { get; set; }

        [Required]
        public required Guid ThreadId { get; set; }

        [ForeignKey("ThreadId")]
        public virtual ForumThread? Thread { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
