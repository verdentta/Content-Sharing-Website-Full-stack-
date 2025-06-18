using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookKeepingWeb.Models
{
    public class UploadContent
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string? Title { get; set; }

        [Required]
        public FileType ContentType { get; set; } // "Image" or "Video"

        [StringLength(500, ErrorMessage = "Description must be 500 or fewer characters")]
        public string? Description { get; set; } = "";  // ✅ Default to empty string

        [Range(0, int.MaxValue)]
        public int? Likes { get; set; }

        public int Views { get; set; } // ✅ New column for tracking views

        public List<Comment> Comments { get; set; } = [];
        public List<Tag> Tags { get; set; } = [];

        public string? ContentPath { get; set; } //  Stores 800x600 resized image
        public string? ThumbnailPath { get; set; } //  Stores 250x200 thumbnail

        public DateTime CreatedDateTime { get; set; } = DateTime.Now;

        [BindNever]
        public string? UserId { get; set; } // Foreign key to UserProfile

        [ForeignKey("UserId")]
        public virtual UserProfile? UserProfile { get; set; } // Navigation property

        // used to determine if the content has been moderated or not, initially set as false for any new content uploaded
        public bool Moderated { get; set; } = false;

        // Related content (self-referencing many-to-many)
        public List<UploadContentRelation> RelatedContents { get; set; } = [];

        [StringLength(100)]
        public string? Slug { get; set; }
    }
}
