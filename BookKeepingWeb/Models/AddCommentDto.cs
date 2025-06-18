namespace BookKeepingWeb.Models
{
    public class AddCommentDto
    {
        public Guid UploadContentId { get; set; }
        public string? CommentContent { get; set; }
    }
}