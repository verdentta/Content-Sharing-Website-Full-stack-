using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookKeepingWeb.Models
{
    public class ForumThread
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Title must be under 100 characters.")]
        public required string Title { get; set; }

        [Required]
        [StringLength(2000, ErrorMessage = "Description must be under 2000 characters.")]
        public required string Description { get; set; }

        public string? ImagePath { get; set; } // Optional image

        [Required]
        public required string CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual UserProfile? CreatedByUser { get; set; } // Remove [Required], make nullable
        public virtual List<ForumComment> Comments { get; set; } = new();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    }
}
