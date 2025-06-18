using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BookKeepingWeb.Models; 

namespace BookKeepingWeb.Models
{
    public class UploadContentRelation
    {
        public Guid UploadContentId { get; set; }
        public UploadContent UploadContent { get; set; }

        public Guid RelatedContentId { get; set; }
        public UploadContent RelatedContent { get; set; }
    }
}
