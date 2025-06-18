namespace BookKeepingWeb.Models
{
    public class ReportCommentDto
    {
        public Guid CommentId { get; set; }
        public required string ReportReason { get; set; }
        public string? AdditionalDetails { get; set; }
    }
}