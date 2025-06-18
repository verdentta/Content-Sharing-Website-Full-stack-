using BookKeepingWeb.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class Comment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string? Content { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    // Foreign key to UserProfile
    [Required]
    public string? UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual UserProfile? User { get; set; } // Navigation property

    // Foreign key to UploadContent
    [Required]
    public Guid UploadContentId { get; set; }

    [ForeignKey("UploadContentId")]
    public virtual UploadContent? UploadContent { get; set; } // Navigation property
}