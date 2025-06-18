using BookKeepingWeb.Data;
using BookKeepingWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.Mvc.Core;
using Microsoft.AspNetCore.Hosting;
using System.IO;


namespace BookKeepingWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _env; // ✅ Added this for file deletion
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IWebHostEnvironment env, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env; // ✅ Store reference to the hosting environment
            _logger = logger;
        }


        // Admin Dashboard
        public IActionResult Index()
        {
            return View();
        }

        // 🔹 2️⃣ Create a New Forum Thread (Only Admins)
        [HttpGet]
        public IActionResult CreateThread()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EditThreads(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalThreads = await _context.ForumThreads.CountAsync();

                var threads = await _context.ForumThreads
                    .OrderByDescending(t => t.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalThreads / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(threads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading forum threads for editing on page {Page}", page);
                TempData["AdminError"] = "Something went wrong while loading the threads. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        // ✅ View Reported Forum Comments
        [HttpGet]
        public async Task<IActionResult> ReportedForumComments(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalReports = await _context.CommentReports
                    .Where(r => r.ForumCommentId != null)
                    .CountAsync();

                var reports = await _context.CommentReports
                    .Include(r => r.ForumComment)
                    .Include(r => r.ReportedByUser)
                    .Where(r => r.ForumCommentId != null)
                    .OrderByDescending(r => r.ReportDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalReports / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reported forum comments on page {Page}", page);
                TempData["AdminError"] = "Unable to load reported comments at the moment. Please try again later.";
                return RedirectToAction("Index");
            }
        }


        // ✅ Resolve a Report (Removes report but keeps the comment)
        [HttpPost]
        public async Task<IActionResult> ResolveForumCommentReport(Guid id)
        {
            try
            {
                var report = await _context.CommentReports.FindAsync(id);
                if (report == null)
                {
                    _logger.LogWarning("Attempted to resolve non-existent forum comment report: {ReportId}", id);
                    return NotFound("Report not found");
                }

                _context.CommentReports.Remove(report);
                await _context.SaveChangesAsync();

                return RedirectToAction("ReportedForumComments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve forum comment report ID: {ReportId}", id);
                TempData["AdminError"] = "Something went wrong while resolving the report.";
                return RedirectToAction("ReportedForumComments");
            }
        }

        // ❌ Delete Reported Forum Comment (Removes comment & all related reports)
        [HttpPost]
        public async Task<IActionResult> DeleteReportedForumComment(Guid commentId)
        {
            try
            {
                var comment = await _context.ForumComments.FindAsync(commentId);
                if (comment == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent forum comment: {CommentId}", commentId);
                    return NotFound("Comment not found");
                }

                // Delete all reports related to this comment
                var reports = _context.CommentReports.Where(r => r.ForumCommentId == commentId);
                _context.CommentReports.RemoveRange(reports);

                // Delete the comment
                _context.ForumComments.Remove(comment);
                await _context.SaveChangesAsync();

                return RedirectToAction("ReportedForumComments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting forum comment and reports for CommentId: {CommentId}", commentId);
                TempData["AdminError"] = "An error occurred while deleting the forum comment.";
                return RedirectToAction("ReportedForumComments");
            }
        }

        // 🔹 1️⃣ View All Uploaded Content
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewAllContent(string searchQuery, int page = 1)
        {
            try
            {
                int pageSize = 10;
                var query = _context.UploadContents.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    query = query.Where(c =>
                        c.Title.Contains(searchQuery) ||
                        c.Id.ToString().Contains(searchQuery));
                }

                var totalItems = await query.CountAsync();

                var contents = await query
                    .OrderByDescending(c => c.CreatedDateTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.SearchQuery = searchQuery;

                return View(contents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load uploaded content list for admin view. Query: {Query}, Page: {Page}", searchQuery, page);
                TempData["AdminError"] = "Something went wrong while loading content. Please try again.";
                return RedirectToAction("Index"); // or a fallback admin page
            }
        }


        // 🔹 2️⃣ Delete Content
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteContent(Guid contentId)
        {
            return await DeleteContentHelper(contentId, "ViewAllContent");
        }

        // 🔹 3️⃣ View All Users
        public async Task<IActionResult> ViewAllUsers(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalUsers = await _userManager.Users.CountAsync();

                var users = await _userManager.Users
                    .OrderBy(u => u.Email)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user list for admin. Page: {Page}", page);
                TempData["AdminError"] = "Unable to load user list. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        // 🔹 4️⃣ Ban (Delete) a User
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> BanUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["AdminError"] = "Invalid user ID.";
                return RedirectToAction("ViewAllUsers");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["AdminError"] = "User not found.";
                    return RedirectToAction("ViewAllUsers");
                }

                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

                if (userProfile != null)
                {
                    if (userProfile.Likes?.Any() == true)
                        userProfile.Likes.Clear();

                    var profilePicturePath = !string.IsNullOrEmpty(userProfile.ProfilePicturePath)
                        ? Path.Combine(_env.WebRootPath, userProfile.ProfilePicturePath.TrimStart('/'))
                        : null;
                    var backgroundImagePath = !string.IsNullOrEmpty(userProfile.BackgroundImagePath)
                        ? Path.Combine(_env.WebRootPath, userProfile.BackgroundImagePath.TrimStart('/'))
                        : null;
                    var defaultFolderPath = Path.Combine(_env.WebRootPath, "default");

                    if (!string.IsNullOrEmpty(profilePicturePath) &&
                        !profilePicturePath.StartsWith(defaultFolderPath) &&
                        System.IO.File.Exists(profilePicturePath))
                    {
                        System.IO.File.Delete(profilePicturePath);
                    }

                    if (!string.IsNullOrEmpty(backgroundImagePath) &&
                        !backgroundImagePath.StartsWith(defaultFolderPath) &&
                        System.IO.File.Exists(backgroundImagePath))
                    {
                        System.IO.File.Delete(backgroundImagePath);
                    }

                    _context.UserProfiles.Remove(userProfile);
                }

                // Delete all related content
                var userContent = await _context.UploadContents.Where(c => c.UserId == userId).ToListAsync();
                foreach (var content in userContent)
                {
                    var relatedAsMain = _context.UploadContentRelation.Where(r => r.UploadContentId == content.Id);
                    var relatedAsRelated = _context.UploadContentRelation.Where(r => r.RelatedContentId == content.Id);

                    _context.UploadContentRelation.RemoveRange(relatedAsMain);
                    _context.UploadContentRelation.RemoveRange(relatedAsRelated);

                    var mainPath = Path.Combine(_env.WebRootPath, content.ContentPath.TrimStart('/'));
                    var thumbPath = !string.IsNullOrEmpty(content.ThumbnailPath)
                        ? Path.Combine(_env.WebRootPath, content.ThumbnailPath.TrimStart('/'))
                        : null;

                    if (System.IO.File.Exists(mainPath)) System.IO.File.Delete(mainPath);
                    if (!string.IsNullOrEmpty(thumbPath) && System.IO.File.Exists(thumbPath)) System.IO.File.Delete(thumbPath);

                    _context.UploadContents.Remove(content);
                }

                var forumComments = await _context.ForumComments.Where(fc => fc.CreatedByUserId == userId).ToListAsync();
                _context.ForumComments.RemoveRange(forumComments);

                var userReports = await _context.CommentReports.Where(r => r.ReportedByUserId == userId || (r.ForumComment != null && r.ForumComment.CreatedByUserId == userId)).ToListAsync();
                _context.CommentReports.RemoveRange(userReports);

                var userComments = await _context.Comments.Where(c => c.UserId == userId).ToListAsync();
                _context.Comments.RemoveRange(userComments);

                // 🛑 Commit all database removals FIRST
                await _context.SaveChangesAsync();

                // ✅ Now delete Identity user
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["AdminError"] = "Failed to delete user account.";
                    return RedirectToAction("ViewAllUsers");
                }

                TempData["AdminSuccess"] = "User banned and all related data removed.";
                return RedirectToAction("ViewAllUsers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while banning user {UserId}", userId);
                TempData["AdminError"] = "Unexpected error occurred while banning the user.";
                return RedirectToAction("ViewAllUsers");
            }
        }



        // 🔹 5️⃣ View Takedown Requests
        public async Task<IActionResult> TakedownRequests(string tab = "not-reviewed", int page = 1)
        {
            try
            {
                int pageSize = 10;
                IQueryable<TakedownRequestModel> query = _context.takedownRequests;

                // Filter
                switch (tab)
                {
                    case "not-reviewed":
                        query = query.Where(tr => !tr.Reviewed);
                        break;
                    case "reviewed":
                        query = query.Where(tr => tr.Reviewed);
                        break;
                }

                query = query.OrderByDescending(tr => tr.CreatedDateTime);

                var totalItems = await query.CountAsync();
                var requests = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.ActiveTab = tab;

                return View(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading takedown requests. Tab: {Tab}, Page: {Page}", tab, page);
                TempData["AdminError"] = "Failed to load takedown requests. Please try again.";
                return RedirectToAction("Index");
            }
        }




        // 🔹 6️⃣ Resolve a Takedown Request
        [HttpPost]
        public async Task<IActionResult> ResolveRequest(Guid id, string tab)
        {
            try
            {
                var takedownRequest = await _context.takedownRequests.FindAsync(id);
                if (takedownRequest == null)
                {
                    _logger.LogWarning("Attempted to resolve non-existent takedown request: {Id}", id);
                    TempData["AdminError"] = "Takedown request not found.";
                    return RedirectToAction("TakedownRequests", new { tab });
                }

                takedownRequest.Reviewed = true;
                await _context.SaveChangesAsync();

                return RedirectToAction("TakedownRequests", new { tab });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve takedown request: {Id}", id);
                TempData["AdminError"] = "An error occurred while resolving the request.";
                return RedirectToAction("TakedownRequests", new { tab });
            }
        }


        // 🔹 7️⃣ Deny a Takedown Request
        [HttpPost]
        public async Task<IActionResult> DenyRequest(Guid id, string tab)
        {
            try
            {
                var request = await _context.takedownRequests.FindAsync(id);
                if (request == null)
                {
                    _logger.LogWarning("Attempted to deny non-existent takedown request: {Id}", id);
                    TempData["AdminError"] = "Takedown request not found.";
                    return RedirectToAction("TakedownRequests", new { tab });
                }

                _context.takedownRequests.Remove(request);
                await _context.SaveChangesAsync();

                return RedirectToAction("TakedownRequests", new { tab });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deny takedown request: {Id}", id);
                TempData["AdminError"] = "An error occurred while denying the request.";
                return RedirectToAction("TakedownRequests", new { tab });
            }
        }


        [HttpPost]
        public async Task<IActionResult> DeleteRequest(Guid id, string tab)
        {
            try
            {
                var takedownRequest = await _context.takedownRequests.FindAsync(id);
                if (takedownRequest == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent takedown request: {Id}", id);
                    TempData["AdminError"] = "Takedown request not found.";
                    return RedirectToAction("TakedownRequests", new { tab });
                }

                _context.takedownRequests.Remove(takedownRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted takedown request: {Id}", id);
                return RedirectToAction("TakedownRequests", new { tab });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting takedown request: {Id}", id);
                TempData["AdminError"] = "An error occurred while deleting the request. Please try again.";
                return RedirectToAction("TakedownRequests", new { tab });
            }
        }



        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ModeratedContent(int page = 1)
        {
            try
            {
                int pageSize = 12;

                var totalItems = await _context.UploadContents.CountAsync(c => c.Moderated);

                var moderatedContent = await _context.UploadContents
                    .Where(c => c.Moderated)
                    .OrderByDescending(c => c.CreatedDateTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(moderatedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load moderated content on page {Page}", page);
                TempData["AdminError"] = "Unable to load moderated content. Please try again.";
                return RedirectToAction("Index");
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnmoderatedContent(int page = 1)
        {
            try
            {
                int pageSize = 12;
                var totalItems = await _context.UploadContents.CountAsync(c => !c.Moderated);

                var unmoderatedContent = await _context.UploadContents
                    .Where(c => !c.Moderated)
                    .OrderByDescending(c => c.CreatedDateTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(unmoderatedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load unmoderated content on page {Page}", page);
                TempData["AdminError"] = "An error occurred while loading unmoderated content. Please try again later.";
                return RedirectToAction("Index");
            }
        }



        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> MarkAsModerated(Guid contentId, int page = 1)
        {
            try
            {
                var content = await _context.UploadContents.FindAsync(contentId);
                if (content == null)
                {
                    _logger.LogWarning("Attempted to moderate non-existent content ID: {ContentId}", contentId);
                    TempData["AdminError"] = "The content you're trying to moderate was not found.";
                    return RedirectToAction("UnmoderatedContent", new { page });
                }

                content.Moderated = true;
                await _context.SaveChangesAsync();
                return RedirectToAction("UnmoderatedContent", new { page });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while marking content ID {ContentId} as moderated.", contentId);
                TempData["AdminError"] = "An error occurred while moderating content. Please try again.";
                return RedirectToAction("UnmoderatedContent", new { page });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUnmoderatedContent(Guid contentId)
        {
            return await DeleteContentHelper(contentId, "UnmoderatedContent");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteModeratedContent(Guid contentId)
        {
            return await DeleteContentHelper(contentId, "ModeratedContent");
        }

        private async Task<IActionResult> DeleteContentHelper(Guid contentId, string redirectAction)
        {
            try
            {
                var content = await _context.UploadContents.FindAsync(contentId);
                if (content == null)
                {
                    _logger.LogWarning("DeleteContentHelper: Content with ID {ContentId} not found.", contentId);
                    TempData["AdminError"] = "The content you're trying to delete was not found.";
                    return RedirectToAction(redirectAction);
                }

                // 🚨 Clean up relations
                var relationsWhereMain = _context.UploadContentRelation
                    .Where(r => r.UploadContentId == contentId);
                var relationsWhereRelated = _context.UploadContentRelation
                    .Where(r => r.RelatedContentId == contentId);

                _context.UploadContentRelation.RemoveRange(relationsWhereMain);
                _context.UploadContentRelation.RemoveRange(relationsWhereRelated);

                // 📁 Delete physical files
                var mainFilePath = Path.Combine(_env.WebRootPath, content.ContentPath.TrimStart('/'));
                var thumbnailFilePath = !string.IsNullOrEmpty(content.ThumbnailPath)
                    ? Path.Combine(_env.WebRootPath, content.ThumbnailPath.TrimStart('/'))
                    : null;

                if (System.IO.File.Exists(mainFilePath))
                {
                    System.IO.File.Delete(mainFilePath);
                    _logger.LogInformation("Deleted main file: {Path}", mainFilePath);
                }

                if (!string.IsNullOrEmpty(thumbnailFilePath) && System.IO.File.Exists(thumbnailFilePath))
                {
                    System.IO.File.Delete(thumbnailFilePath);
                    _logger.LogInformation("Deleted thumbnail file: {Path}", thumbnailFilePath);
                }

                _context.UploadContents.Remove(content);
                await _context.SaveChangesAsync();

                TempData["AdminSuccess"] = "Content was successfully deleted.";
                return RedirectToAction(redirectAction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting content ID: {ContentId}", contentId);
                TempData["AdminError"] = "An error occurred while deleting the content. Please try again.";
                return RedirectToAction(redirectAction);
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTakedownRequest([FromBody] TakedownRequestModel request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Description))
                {
                    return Json(new { success = false, message = "❌ Description is required." });
                }

                var takedown = new TakedownRequestModel
                {
                    PostId = request.PostId,
                    Email = string.IsNullOrEmpty(request.Email) ? "N/A" : request.Email,
                    Description = request.Description,
                    CreatedDateTime = DateTime.UtcNow,
                    Reviewed = false
                };

                _context.takedownRequests.Add(takedown);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "✅ Takedown request submitted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting takedown request for PostId: {PostId}", request?.PostId);
                return Json(new { success = false, message = "❌ Something went wrong. Please try again later." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ReportedComments(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalReports = await _context.CommentReports
                    .Where(r => r.CommentId != null)
                    .CountAsync();

                var reports = await _context.CommentReports
                    .Include(r => r.Comment)
                    .Include(r => r.ReportedByUser)
                    .Where(r => r.CommentId != null)
                    .OrderByDescending(r => r.ReportDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalReports / (double)pageSize);
                ViewBag.CurrentPage = page;

                return View(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reported comments on page {Page}", page);
                TempData["AdminError"] = "Something went wrong while loading reported comments. Please try again.";
                return RedirectToAction("Index");
            }
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ResolveCommentReport(Guid id)
        {
            try
            {
                var report = await _context.CommentReports.FindAsync(id);
                if (report == null)
                {
                    _logger.LogWarning("Attempted to resolve non-existent comment report: {ReportId}", id);
                    TempData["AdminError"] = "Comment report not found.";
                    return RedirectToAction("ReportedComments");
                }

                _context.CommentReports.Remove(report);
                await _context.SaveChangesAsync();

                return RedirectToAction("ReportedComments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving comment report ID: {ReportId}", id);
                TempData["AdminError"] = "An error occurred while resolving the comment report.";
                return RedirectToAction("ReportedComments");
            }
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteReportedComment(Guid commentId)
        {
            try
            {
                var comment = await _context.Comments.FindAsync(commentId);
                if (comment == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent comment: {CommentId}", commentId);
                    TempData["AdminError"] = "Comment not found.";
                    return RedirectToAction("ReportedComments");
                }

                // ❌ Remove all reports related to this comment
                var reports = _context.CommentReports.Where(r => r.CommentId == commentId);
                _context.CommentReports.RemoveRange(reports);

                // ❌ Delete the comment itself
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();

                return RedirectToAction("ReportedComments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting reported comment and its reports. CommentId: {CommentId}", commentId);
                TempData["AdminError"] = "An error occurred while deleting the reported comment.";
                return RedirectToAction("ReportedComments");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateThread(ForumThread model, IFormFile? imageFile)
        {
            ModelState.Remove("CreatedByUser");
            ModelState.Remove("CreatedByUserId");

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning("Thread creation failed due to validation errors: {Errors}", errors);
                ModelState.AddModelError("", "There was a problem creating the thread. Please check your inputs.");
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Thread creation failed. No user ID found.");
                TempData["AdminError"] = "You must be logged in to create a thread.";
                return RedirectToAction("Index", "Forums");
            }

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            if (userProfile == null)
            {
                _logger.LogWarning("Thread creation failed. No user profile found for user ID: {UserId}", userId);
                ModelState.AddModelError("", "User profile not found. Please create a profile first.");
                return View(model);
            }

            model.Id = Guid.NewGuid();
            model.CreatedByUserId = userId;
            model.CreatedDate = DateTime.UtcNow;

            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/threads");
                    Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    model.ImagePath = $"/uploads/threads/{fileName}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading thread image for thread ID: {ThreadId}", model.Id);
                    ModelState.AddModelError("", "Image upload failed. Please try again.");
                    return View(model);
                }
            }

            try
            {
                _context.ForumThreads.Add(model);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New forum thread created. ID: {ThreadId}", model.Id);
                return RedirectToAction("Index", "Forums");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database error while saving thread ID: {ThreadId}", model.Id);
                ModelState.AddModelError("", "An error occurred while saving the thread. Please try again.");
                return View(model);
            }
        }




        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditThread(Guid threadId)
        {
            try
            {
                var thread = await _context.ForumThreads.FindAsync(threadId);
                if (thread == null)
                {
                    _logger.LogWarning("EditThread GET: Thread with ID {ThreadId} not found.", threadId);
                    TempData["AdminError"] = "The requested thread could not be found.";
                    return RedirectToAction("EditThreads");
                }

                var viewModel = new EditForumThreadViewModel
                {
                    Id = thread.Id,
                    Title = thread.Title,
                    Description = thread.Description,
                    ImagePath = thread.ImagePath
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading EditThread for thread ID: {ThreadId}", threadId);
                TempData["AdminError"] = "Something went wrong while loading the thread. Please try again.";
                return RedirectToAction("EditThreads");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditThread(EditForumThreadViewModel model, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                Console.WriteLine("EditThread POST: ModelState invalid: " + string.Join(", ", errors));
                return View(model);
            }

            Console.WriteLine($"EditThread POST: Received Id: {model.Id}");

            var thread = await _context.ForumThreads.FindAsync(model.Id);
            if (thread == null)
            {
                Console.WriteLine($"EditThread POST: Thread with ID {model.Id} not found.");
                return NotFound();
            }

            thread.Title = model.Title;
            thread.Description = model.Description;

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads/threads");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                if (!string.IsNullOrEmpty(thread.ImagePath))
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath, thread.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                thread.ImagePath = $"/uploads/threads/{fileName}";
            }

            try
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"EditThread POST: Thread {model.Id} updated successfully.");
                return RedirectToAction("EditThreads");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EditThread POST: Error saving changes: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while saving changes: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            try
            {
                var thread = await _context.ForumThreads.FindAsync(threadId);
                if (thread == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent thread: {ThreadId}", threadId);
                    TempData["AdminError"] = "Thread not found.";
                    return RedirectToAction("EditThreads");
                }

                // Delete associated image (if it exists and isn't default)
                if (!string.IsNullOrEmpty(thread.ImagePath))
                {
                    var filePath = Path.Combine(_env.WebRootPath, thread.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                            _logger.LogInformation("Deleted image associated with thread {ThreadId}: {ImagePath}", threadId, filePath);
                        }
                        catch (Exception fileEx)
                        {
                            _logger.LogWarning(fileEx, "Failed to delete image file at: {ImagePath}", filePath);
                            // Continue anyway — we still want to delete the thread
                        }
                    }
                }

                _context.ForumThreads.Remove(thread);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Thread {ThreadId} successfully deleted.", threadId);
                return RedirectToAction("EditThreads");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting thread: {ThreadId}", threadId);
                TempData["AdminError"] = "Something went wrong while deleting the thread. Please try again.";
                return RedirectToAction("EditThreads");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ResetUserPasswords(string searchQuery, int page = 1, int pageSize = 10)
        {
            try
            {
                IQueryable<IdentityUser> query = _userManager.Users;

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    query = query.Where(u => u.Email.Contains(searchQuery));
                }

                var totalUsers = await query.CountAsync();

                var users = await query
                    .OrderBy(u => u.Email)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.SearchQuery = searchQuery;

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ResetUserPasswords page. Search: {SearchQuery}, Page: {Page}", searchQuery, page);
                TempData["AdminError"] = "An error occurred while loading the user list. Please try again later.";
                return RedirectToAction("Index");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ResetPasswordForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Invalid user ID." });
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var newPassword = GenerateRandomPassword(12);

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

                if (resetResult.Succeeded)
                {
                    _logger.LogInformation("Admin reset password for user ID: {UserId}", userId);
                    return Json(new { success = true, newPassword });
                }
                else
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to reset password for user ID {UserId}. Errors: {Errors}", userId, errors);
                    return Json(new { success = false, message = "Failed to reset password: " + errors });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while resetting password for user ID: {UserId}", userId);
                return Json(new { success = false, message = "An unexpected error occurred. Please try again." });
            }
        }

        private string GenerateRandomPassword(int length = 12)
        {
            const string upper = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@$?_-";

            Random random = new();

                    // Guarantee at least one of each required character type
                    var password = new List<char>
            {
                upper[random.Next(upper.Length)],
                lower[random.Next(lower.Length)],
                digits[random.Next(digits.Length)],
                special[random.Next(special.Length)]
            };

            // Fill the rest of the password length with a mix of all characters
            string allChars = upper + lower + digits + special;
            for (int i = password.Count; i < length; i++)
            {
                password.Add(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password so guaranteed chars aren’t always at the start
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }






    }
}