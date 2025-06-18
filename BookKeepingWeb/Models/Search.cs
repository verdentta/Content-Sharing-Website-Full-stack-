using System.ComponentModel.DataAnnotations;

namespace BookKeepingWeb.Models
{
    public class Search
    {
        [StringLength(100, ErrorMessage = "Max search length is 100 charactes")]
        public string? SearchInput { get; set; }

        public List<string>? TagNames { get; set; }
    }

}