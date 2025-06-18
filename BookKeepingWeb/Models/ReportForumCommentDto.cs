namespace BookKeepingWeb.Models
{
    public class ReportForumCommentDto
    {
        public Guid? ForumCommentId { get; set; }
        public string ReportReason { get; set; } = string.Empty;
        public string? AdditionalDetails { get; set; }
    }
}