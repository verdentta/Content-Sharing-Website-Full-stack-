using System.ComponentModel.DataAnnotations;

namespace BookKeepingWeb.Models
{
    public class UserProfile
    {
        [Key]
        public string? UserId { get; set; } // Foreign key to IdentityUser

        public string? ProfilePicturePath { get; set; } // Path for uploaded profile picture

        public string? BackgroundImagePath { get; set; } // Path for uploaded background image

        [StringLength(25, ErrorMessage = "Screen Name cannot exceed 25 characters.")]
        public string? ScreenName { get; set; } // Custom screen name

        [StringLength(500, ErrorMessage = "Description is too long")]
        public string? Description { get; set; } // Short user description

        public string? Country { get; set; } // Country of residence

        [Range(18, 120)]
        public int? Age { get; set; } // Required user age

        public List<Guid>? Likes { get; set; } = [];

        // Navigation property
        public virtual ICollection<UploadContent>? UploadContents { get; set; }
    }
}
