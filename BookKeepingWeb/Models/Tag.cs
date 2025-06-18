using System.ComponentModel.DataAnnotations;

namespace BookKeepingWeb.Models
{
    public class Tag
    {
        [Key]
        public Guid TagId { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Tag length must be 20 or less characters")]
        public string? Name { get; set; }

        [Range(1, int.MaxValue)]
        public int Count { get ; set; }

        public List<UploadContent> UploadContents { get; set; } = [];
    }
}
