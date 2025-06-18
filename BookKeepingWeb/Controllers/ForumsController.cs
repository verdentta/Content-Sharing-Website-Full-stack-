using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookKeepingWeb.Data;
using BookKeepingWeb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using BookKeepingWeb.Controllers;
using Ganss.Xss;
using Microsoft.AspNetCore.RateLimiting;

public class ForumsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<HomeController> _logger;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly AccountValidationService _validationService;

    public ForumsController(SignInManager<IdentityUser> signInManager, AccountValidationService validationService, ILogger<HomeController> logger, ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _validationService = validationService;
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var threads = await _context.ForumThreads
                .Include(t => t.CreatedByUser)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return View(threads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading forum threads in ForumsController.Index.");
            TempData["ForumError"] = "Unable to load threads at the moment. Please try again later.";
            return RedirectToAction("Index", "Home"); // Or redirect to a fallback forum-safe view
        }
    }

    public async Task<IActionResult> DetailedThread(Guid id, int page = 1)
    {
        const int PageSize = 12;

        try
        {
            if (page < 1) page = 1;

            var thread = await _context.ForumThreads
                .Include(t => t.CreatedByUser)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread == null)
            {
                _logger.LogWarning("DetailedThread: Thread with ID {ThreadId} not found.", id);
                return NotFound();
            }

            var totalComments = thread.Comments.Count;
            var totalPages = (int)Math.Ceiling(totalComments / (double)PageSize);
            if (page > totalPages) page = totalPages == 0 ? 1 : totalPages;

            var paginatedComments = thread.Comments
                .OrderBy(c => c.CreatedDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            thread.Comments = paginatedComments;

            return View(thread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading thread details for ID {ThreadId} on page {Page}", id, page);
            TempData["ForumError"] = "An error occurred while loading the thread. Please try again later.";
            return RedirectToAction("Index");
        }
    }


 
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [EnableRateLimiting("CommentLimiter")]
    public async Task<IActionResult> AddComment(Guid threadId, string content)
    {
        //validate if they're not a banned user or they exist in the db
        if (!await _validationService.IsUserStillValid(User))
        {
            await _signInManager.SignOutAsync();
            TempData["BanNotice"] = "Your account has been banned.";
            return RedirectToAction("Banned", "Home");
        }

        try
        {
            content = content?.Trim() ?? "";

            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedAttributes.Clear();

            content = sanitizer.Sanitize(content);

            if (content.Length > 500)
            {
                content = content.Substring(0, 500);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ForumError"] = "Comment cannot be empty.";
                return RedirectToAction("DetailedThread", new { id = threadId });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("AddComment: Unauthorized access attempt.");
                return Unauthorized();
            }

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            if (userProfile == null)
            {
                _logger.LogWarning("AddComment: No profile found for user {UserId}", userId);
                return Unauthorized("User profile not found.");
            }

            var comment = new ForumComment
            {
                Id = Guid.NewGuid(),
                ThreadId = threadId,
                Content = content,
                CreatedByUserId = userId,
                CreatedByUser = userProfile,
                CreatedDate = DateTime.UtcNow
            };

            _context.ForumComments.Add(comment);
            await _context.SaveChangesAsync();

            int commentsBefore = await _context.ForumComments
                .Where(c => c.ThreadId == threadId && c.CreatedDate < comment.CreatedDate)
                .CountAsync();

            int commentsPerPage = 12;
            int pageNumber = (commentsBefore / commentsPerPage) + 1;

            _logger.LogInformation("User {UserId} added comment {CommentId} to thread {ThreadId}", userId, comment.Id, threadId);

            return Redirect($"/Forums/DetailedThread/{threadId}?page={pageNumber}#comment-{comment.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while adding comment to thread {ThreadId}", threadId);
            TempData["ForumError"] = "An error occurred while adding your comment. Please try again.";
            return RedirectToAction("DetailedThread", new { id = threadId });
        }
    }


    
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [EnableRateLimiting("ReportLimiter")]
    public async Task<IActionResult> ReportForumComment([FromBody] ReportForumCommentDto? reportDto)
    {
        //validate if they're not a banned user or they exist in the db
        if (!await _validationService.IsUserStillValid(User))
        {
            _logger.LogWarning("ReportForumComment: Unauthorized report attempt.");
            return Unauthorized(new { success = false, message = "You must be logged in to add a comment." });
        }

        try
        {
            if (reportDto == null)
            {
                _logger.LogWarning("ReportForumComment: Null reportDto received.");
                return BadRequest(new { success = false, message = "Invalid report data received." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("ReportForumComment: Unauthorized report attempt.");
                return Unauthorized(new { success = false, message = "You must be logged in to report forum comments." });
            }

            var details = reportDto.AdditionalDetails?.Trim() ?? "";
            details = System.Net.WebUtility.HtmlEncode(details);

            if (details.Length > 150)
            {
                details = details.Substring(0, 150);
            }

            // Optional: check if the comment exists before reporting it
            var commentExists = await _context.ForumComments.AnyAsync(c => c.Id == reportDto.ForumCommentId);
            if (!commentExists)
            {
                _logger.LogWarning("ReportForumComment: Attempted to report non-existent comment {CommentId}", reportDto.ForumCommentId);
                return NotFound(new { success = false, message = "The comment you're trying to report does not exist." });
            }

            var report = new CommentReport
            {
                Id = Guid.NewGuid(),
                ForumCommentId = reportDto.ForumCommentId,
                ReportedByUserId = userId,
                ReportReason = reportDto.ReportReason,
                AdditionalDetails = details
            };

            _context.CommentReports.Add(report);
            await _context.SaveChangesAsync();

            _logger.LogInformation("ReportForumComment: User {UserId} reported comment {CommentId}", userId, reportDto.ForumCommentId);
            return Ok(new { success = true, message = "Forum comment report submitted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting forum comment {CommentId}", reportDto?.ForumCommentId);
            return StatusCode(500, new { success = false, message = "An error occurred while submitting the report. Please try again." });
        }
    }





}
