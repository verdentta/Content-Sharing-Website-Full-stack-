using System.ComponentModel.DataAnnotations;

namespace BookKeepingWeb.Models
{
    public class TakedownRequestModel
    {
        [Key]
        public Guid Id { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public bool Reviewed { get; set; } = false;

        [Required]
        [StringLength(500, ErrorMessage = "Max character length is 500 characters.")]
        public string? Description { get; set; }

        [Required]
        public string? PostId { get; set; }
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow; // used for ordering in the admin panel

    }
}
