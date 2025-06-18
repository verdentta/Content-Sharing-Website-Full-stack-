using System.Collections.Generic;

namespace BookKeepingWeb.Models
{
    public class ModerateContentViewModel
    {
        public List<UploadContent> UnmoderatedContent { get; set; } = new();
        public List<UploadContent> ModeratedContent { get; set; } = new();

        // Pagination properties for Unmoderated Content
        public int UnmoderatedPage { get; set; }
        public int TotalUnmoderatedPages { get; set; }

        // Pagination properties for Moderated Content
        public int ModeratedPage { get; set; }
        public int TotalModeratedPages { get; set; }
    }
}