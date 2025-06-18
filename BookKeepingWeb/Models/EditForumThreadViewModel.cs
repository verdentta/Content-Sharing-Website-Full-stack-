using System.ComponentModel.DataAnnotations;

namespace BookKeepingWeb.Models
{
    public class EditForumThreadViewModel
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Title must be under 100 characters.")]
        public required string Title { get; set; }

        [Required]
        [StringLength(2000, ErrorMessage = "Description must be under 2000 characters.")]
        public required string Description { get; set; }

        public string? ImagePath { get; set; } // For displaying the current image
    }
}