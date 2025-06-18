using System.Diagnostics;
using BookKeepingWeb.Data; // Add this for database access
using BookKeepingWeb.Models;
using BookKeepingWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookKeepingWeb.Helpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace BookKeepingWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly AccountValidationService _validationService;
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db; // Add the database context
        private readonly ImageService _imageService;
        private readonly IWebHostEnvironment _env;

        public HomeController(SignInManager<IdentityUser> signInManager, AccountValidationService validationService, ILogger<HomeController> logger, ApplicationDbContext db, ImageService imageService, IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _validationService = validationService;
            _logger = logger;
            _db = db;
            _imageService = imageService;
            _env = env;
        }

        /// <summary>
        /// Display the home page
        /// </summary>
        /// <returns></returns>
        //public IActionResult Index()
        //{
        //    return View();
        //}

        /// <summary>
        /// Display the privacy page
        /// </summary>
        /// <returns></returns>
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult ForgotPasswordInfo()
        {
            return View();
        }

        public IActionResult Banned()
        {
            return View(); // ← looks for "Banned.cshtml"
        }

        public IActionResult RateLimit()
        {
            return View(); // Looks for Views/Home/RateLimit.cshtml or Views/Shared/RateLimit.cshtml
        }

        /// <summary>
        /// return the takedown request confimation page
        /// </summary>
        /// <returns></returns>
        //public IActionResult Confirmation()
        //{
        //    return View();
        //}

        /// <summary>
        /// Display the terms of service page
        /// </summary>
        /// <returns></returns>
        //public IActionResult TermsOfService()
        //{
        //    return View();
        //}

        /// <summary>
        /// Display the takedown page
        /// </summary>
        /// <returns></returns>
        //public IActionResult Takedown()
        //{
        //    return View();
        //}

        /// <summary>
        /// Display the error page
        /// </summary>
        /// <returns></returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Get a list of tags given a json string
        /// </summary>
        /// <param name="id">the id of the tag</param>
        /// <returns>json containing the tags</returns>
        [HttpGet]
        public async Task<IActionResult> GetPostFromTag(string id)
        {
            try
            {
                var temp = Guid.Parse(id);
                var context = await _db.Tags
                        .Include(i => i.UploadContents)
                        .SingleOrDefaultAsync(i => i.TagId == temp);

                if (context == null)
                    return Json(new { contents = new List<string>() });

                return Json(new { contents = context.UploadContents.Select(i => i.Title).ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get tag content for tag ID: {TagId}", id);
                return Json(new { contents = new List<string>() });
            }
        }

        /// <summary>
        /// Get a list of content given the page and the number of posts per page
        /// </summary>
        /// <param name="page">the page the user is on</param>
        /// <param name="pageSize">number of posts per page</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetContent(int page = 1, int pageSize = 9)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var contentList = await _db.UploadContents
                    .OrderByDescending(c => c.CreatedDateTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userLikes = userId != null
                    ? await _db.UserProfiles
                        .Where(u => u.UserId == userId)
                        .SelectMany(u => u.Likes)
                        .ToListAsync()
                    : new List<Guid>();

                var response = contentList.Select(content => new
                {
                    Id = content.Id,
                    ContentPath = content.ContentPath,
                    ThumbnailPath = content.ThumbnailPath,
                    ContentType = content.ContentType,
                    Title = content.Title,
                    CreatedDateTime = content.CreatedDateTime,
                    Likes = content.Likes,
                    LikedByUser = userLikes.Contains(content.Id)
                }).ToList();

                int totalCount = await _db.UploadContents.CountAsync();
                bool hasMore = (page * pageSize) < totalCount;

                return Json(new { contents = response, hasMore });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetContent for page {Page}", page);
                return Json(new { contents = new List<object>(), hasMore = false });
            }
        }


        /// <summary>
        /// Get the details of a given post
        /// </summary>
        /// <param name="id">the id of a post</param>
        /// <returns></returns>
        [HttpGet("Details/{id:guid}/{slug?}")]
        public async Task<IActionResult> Details(Guid id, string? slug)
        {
            try
            {
                var uploadContent = await _db.UploadContents
                    .Include(uc => uc.Comments)
                        .ThenInclude(c => c.User)
                    .Include(i => i.Tags)
                    .FirstOrDefaultAsync(uc => uc.Id == id);

                if (uploadContent == null)
                {
                    return NotFound();
                }

                // ✅ If the slug is missing or wrong, redirect to canonical URL
                if (string.IsNullOrEmpty(slug) || slug != uploadContent.Slug)
                {
                    return RedirectToAction("Details", new { id = uploadContent.Id, slug = uploadContent.Slug });
                }

                // Prevent duplicate views
                string viewKey = $"Viewed_{id}";
                if (!HttpContext.Session.Keys.Contains(viewKey))
                {
                    uploadContent.Views += 1;
                    await _db.SaveChangesAsync();
                    HttpContext.Session.SetString(viewKey, "true");
                }

                // Author info
                var author = await _db.UserProfiles
                    .FirstOrDefaultAsync(u => u.UserId == uploadContent.UserId);

                ViewBag.Author = author?.ScreenName ?? "Unknown Author";
                ViewBag.AuthorProfilePicture = author?.ProfilePicturePath ?? "/default/default-profile.png";

                // Like status
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                ViewBag.IsLikedByUser = userId != null &&
                    await _db.UserProfiles
                        .Where(u => u.UserId == userId)
                        .AnyAsync(u => u.Likes.Contains(uploadContent.Id));

                // Load existing related content entries
                var existingRelations = await _db.UploadContentRelation
                    .Include(r => r.RelatedContent)
                        .ThenInclude(rc => rc.Tags)
                    .Where(r => r.UploadContentId == uploadContent.Id)
                    .ToListAsync();

                var validRelated = existingRelations
                    .Where(r => r.RelatedContent != null)
                    .Select(r => r.RelatedContent)
                    .ToList();

                int missingCount = 16 - validRelated.Count;

                if (missingCount > 0)
                {
                    var excludedIds = validRelated.Select(r => r.Id).ToList();

                    var replacements = await _db.UploadContents
                        .Include(c => c.Tags)
                        .Where(c =>
                            c.Id != uploadContent.Id &&
                            !excludedIds.Contains(c.Id) &&
                            c.Tags.Any(t => uploadContent.Tags.Select(ut => ut.Name).Contains(t.Name)))
                        .OrderByDescending(c => c.Tags.Count(t => uploadContent.Tags.Select(ut => ut.Name).Contains(t.Name)))
                        .ThenByDescending(c => c.CreatedDateTime)
                        .Take(missingCount)
                        .ToListAsync();

                    var newRelations = replacements.Select(r => new UploadContentRelation
                    {
                        UploadContentId = uploadContent.Id,
                        RelatedContentId = r.Id
                    });

                    await _db.UploadContentRelation.AddRangeAsync(newRelations);
                    await _db.SaveChangesAsync();

                    validRelated.AddRange(replacements);
                }

                if (validRelated.Any())
                {
                    ViewBag.RelatedContent = validRelated;
                }
                else
                {
                    ViewBag.PopularContent = await _db.UploadContents
                        .OrderByDescending(c => c.Likes)
                        .ThenByDescending(c => c.CreatedDateTime)
                        .Take(16)
                        .ToListAsync();
                }

                return View(uploadContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading content details for ID: {Id}", id);
                return RedirectToAction("Error", "Home");
            }
        }




        /// <summary>
        /// Display the paginated view of all content
        /// </summary>
        /// <param name="page">the page the user is on</param>
        /// <param name="pageSize">the number of posts per page</param>
        /// <returns></returns>
        public async Task<IActionResult> PaginatedView(
        int page = 1,
        int pageSize = 20,
        List<string> SelectedTags = null,
        List<FileType> SelectedFileTypes = null)
        {
            try
            {
                if (page < 1) page = 1;

                SelectedTags ??= new List<string>();
                SelectedFileTypes ??= new List<FileType>();

                IQueryable<UploadContent> query = _db.UploadContents
                    .Include(c => c.Tags)
                    .OrderByDescending(c => c.CreatedDateTime)
                    .AsNoTracking();

                // Filter by Selected Tags
                if (SelectedTags.Any())
                {
                    query = query.Where(c => SelectedTags.All(tag => c.Tags.Select(t => t.Name).Contains(tag)));
                }

                // Filter by Selected File Types
                if (SelectedFileTypes.Any())
                {
                    query = query.Where(c => SelectedFileTypes.Contains(c.ContentType));
                }

                // Paginate
                var paginatedList = await PaginatedList<UploadContent>.CreateAsync(query, page, pageSize);

                // Pass data to the view
                ViewData["CurrentPage"] = page;
                ViewData["TotalPages"] = paginatedList.TotalPages;
                ViewData["SelectedTags"] = SelectedTags;
                ViewData["SelectedFileTypes"] = SelectedFileTypes;
                ViewData["PageSize"] = pageSize;

                return View(paginatedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading the paginated view.");
                TempData["Error"] = "Something went wrong while loading content. Please try again later.";
                return RedirectToAction("Error", "Home");
            }
        }


        /// <summary>
        /// Add a comment to a post
        /// </summary>
        /// <param name="model">comment model</param>
        /// <returns></returns>
        [EnableRateLimiting("CommentLimiter")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddComment([FromBody] AddCommentDto model)
        {
            //validate if they're not a banned user or they exist in the db
            if (!await _validationService.IsUserStillValid(User))
            {
                _logger.LogWarning("DetailComment: Unauthorized report attempt.");
                return Unauthorized(new { success = false, message = "You must be logged in to add a comment." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                // ✅ Trim and fallback to empty string
                var content = model.CommentContent?.Trim() ?? "";

                // ✅ Sanitize to plain text (HTML encode)
                var sanitizer = new HtmlSanitizer();
                sanitizer.AllowedTags.Clear();
                sanitizer.AllowedAttributes.Clear();
                content = sanitizer.Sanitize(content);

                // ✅ Enforce 500 char limit
                if (content.Length > 500)
                {
                    content = content.Substring(0, 500);
                }

                // ✅ Don’t allow empty strings
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Json(new { success = false, message = "Comment cannot be empty!" });
                }

                var comment = new Comment
                {
                    Content = content,
                    UploadContentId = model.UploadContentId,
                    UserId = userId,
                    Date = DateTime.UtcNow
                };

                _db.Comments.Add(comment);
                await _db.SaveChangesAsync();

                var user = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

                return Json(new
                {
                    success = true,
                    comment = new
                    {
                        userId = comment.UserId,
                        content = comment.Content,
                        date = comment.Date.ToString("MMM dd, yyyy"),
                        userName = user?.ScreenName ?? "Unknown User"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while adding comment to post ID: {PostId}", model.UploadContentId);
                return Json(new { success = false, message = "❌ Failed to post your comment. Please try again." }); //  since we're using ajax here
            }
        }



        /// <summary>
        /// Report a post
        /// </summary>
        /// <param name="obj">the takedown request object</param>
        /// <returns></returns>
        [HttpPost]
        [EnableRateLimiting("ReportLimiter")]
        public async Task<IActionResult> TakedownRequest([FromBody] TakedownRequestModel obj)
        {
            if (obj == null)
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            // ✅ Trim and sanitize inputs
            obj.Email = System.Net.WebUtility.HtmlEncode(obj.Email?.Trim() ?? "");
            obj.Description = System.Net.WebUtility.HtmlEncode(obj.Description?.Trim() ?? "");

            if (string.IsNullOrWhiteSpace(obj.Description))
            {
                return Json(new { success = false, message = "Please enter a reason for the post being reported." });
            }

            if (string.IsNullOrWhiteSpace(obj.PostId))
            {
                return Json(new { success = false, message = "Invalid post ID." });
            }

            // ✅ Enforce length limits
            if (obj.Email.Length > 100)
                obj.Email = obj.Email[..100];

            if (obj.Description.Length > 300)
                obj.Description = obj.Description[..300];

            try
            {
                // ✅ Extract GUID from PostId URL
                var segments = obj.PostId.Split("/", StringSplitOptions.RemoveEmptyEntries);
                var detailsIndex = Array.FindIndex(segments, s => s.Equals("Details", StringComparison.OrdinalIgnoreCase));

                if (detailsIndex == -1 || detailsIndex + 1 >= segments.Length)
                {
                    _logger.LogWarning("Invalid PostId URL format: {PostId}", obj.PostId);
                    return Json(new { success = false, message = "Invalid post URL." });
                }

                string idPart = segments[detailsIndex + 1];

                if (!Guid.TryParse(idPart, out Guid postId))
                {
                    _logger.LogWarning("Failed to parse PostId as GUID: {IdPart}", idPart);
                    return Json(new { success = false, message = "Invalid post ID format." });
                }

                var post = await _db.UploadContents.SingleOrDefaultAsync(u => u.Id == postId);
                if (post == null)
                {
                    return Json(new { success = false, message = "Post not found." });
                }

                var request = new TakedownRequestModel
                {
                    Id = Guid.NewGuid(),
                    Email = obj.Email,
                    Description = obj.Description,
                    PostId = post.Id.ToString()
                };

                _db.takedownRequests.Add(request);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "✅ Takedown request submitted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process takedown request for PostId: {PostId}", obj.PostId);
                return Json(new { success = false, message = "❌ Failed to submit the request. Please try again." });
            }
        }



        /// <summary>
        /// Like a post
        /// </summary>
        /// <param name="likeTdo">like model</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> ToggleLike([FromBody] LikeTdo likeTdo)
        {

            try
            {
                _logger.LogInformation("ToggleLike called with ContentId: {ContentId}", likeTdo.ContentId);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("Using userId: {UserId}", userId);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized attempt to like content.");
                    return Unauthorized(new { success = false, message = "You must be logged in to like content." });
                }

                var user = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                {
                    _logger.LogWarning("User profile not found for userId: {UserId}", userId);
                    return NotFound(new { success = false, message = "User profile not found." });
                }

                var content = await _db.UploadContents.FirstOrDefaultAsync(c => c.Id == likeTdo.ContentId);
                if (content == null)
                {
                    _logger.LogWarning("Content not found for ContentId: {ContentId}", likeTdo.ContentId);
                    return NotFound(new { success = false, message = "Content not found." });
                }

                // Toggle like
                bool alreadyLiked = user.Likes.Contains(likeTdo.ContentId);
                if (alreadyLiked)
                {
                    user.Likes.Remove(likeTdo.ContentId);
                    content.Likes = Math.Max((content.Likes ?? 0) - 1, 0);
                }
                else
                {
                    user.Likes.Add(likeTdo.ContentId);
                    content.Likes = (content.Likes ?? 0) + 1;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("ToggleLike completed. LikesCount: {LikesCount}, Liked: {!alreadyLiked}", content.Likes, !alreadyLiked);

                return Json(new
                {
                    success = true,
                    likesCount = content.Likes,
                    liked = !alreadyLiked
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling like for ContentId: {ContentId}", likeTdo.ContentId);
                return Json(new { success = false, message = "❌ Something went wrong while liking the content." });
            }
        }


        [HttpPost]
        [Authorize]
        [EnableRateLimiting("ReportLimiter")]
        public async Task<IActionResult> ReportComment([FromBody] ReportCommentDto? reportDto)
        {
            //validate if they're not a banned user or they exist in the db
            if (!await _validationService.IsUserStillValid(User))
            {
                _logger.LogWarning("ReportForumComment: Unauthorized report attempt.");
                return Unauthorized(new { success = false, message = "You must be logged in to report comments!" });
            }

            if (reportDto == null)
            {
                _logger.LogWarning("Received a null reportDto in ReportComment.");
                return BadRequest(new { success = false, message = "Invalid report data received." });
            }

            try
            {
                _logger.LogInformation("Received report: {CommentId} - {Reason}", reportDto.CommentId, reportDto.ReportReason);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unauthorized user tried to report a comment.");
                    return Unauthorized(new { success = false, message = "You must be logged in to report comments." });
                }

                var comment = await _db.Comments.FindAsync(reportDto.CommentId);
                if (comment == null)
                {
                    _logger.LogWarning("Comment not found: {CommentId}", reportDto.CommentId);
                    return NotFound(new { success = false, message = "Comment not found." });
                }

                var details = reportDto.AdditionalDetails?.Trim() ?? "";
                details = System.Net.WebUtility.HtmlEncode(details);

                if (details.Length > 150)
                {
                    details = details.Substring(0, 150);
                }

                var report = new CommentReport
                {
                    Id = Guid.NewGuid(),
                    CommentId = reportDto.CommentId,
                    ReportedByUserId = userId,
                    ReportReason = reportDto.ReportReason,
                    AdditionalDetails = details
                };

                _db.CommentReports.Add(report);
                await _db.SaveChangesAsync();

                return Ok(new { success = true, message = "Comment report submitted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reporting comment: {CommentId}", reportDto.CommentId);
                return StatusCode(500, new { success = false, message = "❌ Something went wrong while reporting this comment." });
            }
        }


        public async Task<IActionResult> Homepage(int page = 1, int pageSize = 20)
        {
            try
            {
                // Global fix: sanitize incoming page number
                if (page < 1) page = 1;

                if (page == 1)
                {
                    // Show Featured Content
                    ViewBag.MostLiked = await _db.UploadContents
                        .OrderByDescending(c => c.Likes)
                        .Take(8)
                        .ToListAsync();

                    ViewBag.MostViewed = await _db.UploadContents
                        .OrderByDescending(c => c.Views)
                        .Take(8)
                        .ToListAsync();

                    ViewBag.MostRecent = await _db.UploadContents
                        .OrderByDescending(c => c.CreatedDateTime)
                        .Take(8)
                        .ToListAsync();

                    ViewBag.LatestThread = await _db.ForumThreads
                        .Include(t => t.CreatedByUser)
                        .OrderByDescending(t => t.CreatedDate)
                        .FirstOrDefaultAsync();

                    int totalCount = await _db.UploadContents.CountAsync();
                    ViewBag.CurrentPage = 1;
                    ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize) + 1; // Add 1 to include the special page
                }
                else
                {
                    // Logical page = actual page - 1
                    int logicalPage = page - 1;

                    IQueryable<UploadContent> query = _db.UploadContents
                        .OrderByDescending(c => c.CreatedDateTime)
                        .AsNoTracking();

                    var paginatedList = await PaginatedList<UploadContent>.CreateAsync(query, logicalPage, pageSize);

                    ViewBag.PaginatedContent = paginatedList;
                    ViewBag.CurrentPage = page;
                    ViewBag.TotalPages = paginatedList.TotalPages + 1; // Add 1 to match with special page
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading homepage content.");
                ViewBag.LoadError = "Could not load content at this time.";
            }

            return View();
        }




        [HttpGet]
        public async Task<IActionResult> Stream(Guid id)
        {
            try
            {
                var content = await _db.UploadContents.FirstOrDefaultAsync(c => c.Id == id);
                if (content == null || content.ContentType != FileType.Video)
                {
                    _logger.LogWarning("Stream request failed: Content not found or not a video. Id: {Id}", id);
                    return NotFound();
                }

                var filePath = Path.Combine(_env.WebRootPath, content.ContentPath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Stream request failed: File not found on disk for Id: {Id}", id);
                    return NotFound();
                }

                if (!Request.Headers.ContainsKey("Range"))
                {
                    _logger.LogWarning("Stream blocked: No Range header for content Id: {Id}", id);
                    TempData["AccessReason"] = "Unauthorized attempt to stream restricted content.";
                    return View("AccessDenied");
                }

                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, "video/mp4", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while streaming video with ID: {Id}", id);
                return StatusCode(500, "❌ Error while streaming the requested video.");
            }
        }








    }
}
