using BookKeepingWeb.Data;
using BookKeepingWeb.Helpers;
using BookKeepingWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookKeepingWeb.Controllers
{
    public class SearchController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        public SearchController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// return the index page
        /// </summary>
        /// <returns>the search page</returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Get a list of tags given a json string
        /// </summary>
        /// <param name="json">string containing json</param>
        /// <returns>a list of guids of tags</returns>
        private async Task<List<Guid>> GetTagsFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<Guid>();

            // Split and filter out empty values caused by consecutive "#"
            var tagNames = json.Split("#", StringSplitOptions.RemoveEmptyEntries).ToList();
            tagNames.RemoveAt(0);
            var tagList = new List<Guid>();

            foreach (var tagName in tagNames)
            {
                // Fetch tag by name
                var tag = await _context.Tags.SingleOrDefaultAsync(t => t.Name == tagName.Trim());
                if (tag != null)
                {
                    tagList.Add(tag.TagId);
                }
            }

            return tagList;
        }

        /// <summary>
        /// Stem the word
        /// </summary>
        /// <param name="word">the word</param>
        /// <returns>a string if the stem word</returns>
        private string StemWord(string word)
        {
            // Basic manual stemming example (remove trailing 's')
            if (word.EndsWith("s"))
            {
                return word.Substring(0, word.Length - 1);
            }
            return word;
        }

        /// <summary>
        /// removes the # for working on tags
        /// </summary>
        /// <param name="word">tag</param>
        /// <returns>the tag without the #'s</returns>
        private string RemoveHash(string word)
        {
            // Remove the leading '#' if it exists
            if (!string.IsNullOrEmpty(word) && word.StartsWith("#"))
            {
                return word.Substring(1); // Remove the '#' character
            }
            return word;
        }

        /// <summary>
        /// Search for content based on the search term and media type
        /// </summary>
        /// <param name="search">Search string</param>
        /// <param name="media">which type of media the user is looking for</param>
        /// <param name="page">the page the user is on</param>
        /// <param name="pageSize">the size of each page</param>
        /// <returns>A view given the parameters</returns>
        [HttpGet]
        [Route("Search/SearchAsync")]
        public IActionResult Search(string search, List<FileType> media, int page = 1, int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;

                IQueryable<UploadContent> query = _context.UploadContents
                .Include(c => c.Tags)
                .Include(c => c.UserProfile) // Assuming UserProfile is the navigation property for the user
                .OrderByDescending(c => c.CreatedDateTime);

                // Filter by selected media types
                if (media != null && media.Any())
                {
                    query = query.Where(c => media.Contains(c.ContentType));
                }

                // Search logic
                if (!string.IsNullOrEmpty(search))
                {
                    // Extract text after the last '#'
                    string cleanedSearch = search.Contains('#')
                        ? search.Substring(search.LastIndexOf('#') + 1).Trim()
                        : search.Trim();

                    // If no valid text remains, return no results
                    if (string.IsNullOrEmpty(cleanedSearch))
                    {
                        return View(new PaginatedList<UploadContent>(new List<UploadContent>(), 0, page, pageSize));
                    }

                    // Remove leading/trailing whitespace and perform stemming
                    string stemmedSearch = StemWord(cleanedSearch); // Stem the search term

                    query = query.Where(c =>
                            EF.Functions.Like(c.Title, $"%{cleanedSearch}%") ||          // Match full search term in Title
                            EF.Functions.Like(c.Title, $"%{stemmedSearch}%") ||         // Match stemmed word in Title
                            EF.Functions.Like(c.Description, $"%{cleanedSearch}%") ||   // Match full search term in Description
                            EF.Functions.Like(c.Description, $"%{stemmedSearch}%") ||   // Match stemmed word in Description
                            c.Tags.Any(t => EF.Functions.Like(t.Name, $"%{cleanedSearch}%") ||
                                EF.Functions.Like(t.Name, $"%{stemmedSearch}%")) ||     // Match Tags
                            EF.Functions.Like(c.UserProfile!.ScreenName, $"%{cleanedSearch}%")); // Match ScreenName
                }

                // Pagination logic
                var paginatedList = PaginatedList<UploadContent>.Create(query, page, pageSize);
                if (page > paginatedList.TotalPages)
                    page = paginatedList.TotalPages == 0 ? 1 : paginatedList.TotalPages;

                ViewData["Search"] = search;
                ViewData["CurrentPage"] = page;
                ViewData["TotalPages"] = paginatedList.TotalPages;

                return View(paginatedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search. Search: {Search}, Media: {Media}", search, media);
                TempData["SearchError"] = "Something went wrong while searching. Please try again.";
                return RedirectToAction("Index");
            }

        }

    }
}
