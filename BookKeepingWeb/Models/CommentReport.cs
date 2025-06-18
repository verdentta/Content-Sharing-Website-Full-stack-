using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookKeepingWeb.Models
{
    public class CommentReport
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? CommentId { get; set; } // For content comments
        public Guid? ForumCommentId { get; set; } // 🟢 for forum comments

        [ForeignKey("CommentId")]
        public virtual Comment? Comment { get; set; }

        [ForeignKey("ForumCommentId")]
        public virtual ForumComment? ForumComment { get; set; } // ✅ Now correctly linked

        [Required]
        public required string ReportedByUserId { get; set; }

        [ForeignKey("ReportedByUserId")]
        public virtual UserProfile? ReportedByUser { get; set; }

        [Required]
        public required string ReportReason { get; set; }

        public string? AdditionalDetails { get; set; }

        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    }
}
