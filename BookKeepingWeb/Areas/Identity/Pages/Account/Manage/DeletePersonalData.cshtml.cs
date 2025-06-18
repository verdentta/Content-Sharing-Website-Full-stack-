using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BookKeepingWeb.Data;
using BookKeepingWeb.Models;
using Microsoft.AspNetCore.Hosting;

namespace BookKeepingWeb.Areas.Identity.Pages.Account.Manage
{
    public class DeletePersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<DeletePersonalDataModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env; // For file deletion

        public DeletePersonalDataModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<DeletePersonalDataModel> logger,
            ApplicationDbContext context,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _env = env; // Store reference to the hosting environment
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public bool RequirePassword { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            if (RequirePassword)
            {
                if (string.IsNullOrWhiteSpace(Input.Password)) // ✅ Prevents empty input crash
                {
                    ModelState.AddModelError(string.Empty, "Password is required.");
                    return Page();
                }

                if (!await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Incorrect password.");
                    return Page();
                }
            }

            var userId = await _userManager.GetUserIdAsync(user);

            // 🔹 STEP 1: Delete the User’s Profile, Likes & Files
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (userProfile != null)
            {
                // 🚨 Define the default folder where default images are stored
                string defaultFolderPath = Path.Combine(_env.WebRootPath, "default");

                // ✅ Delete profile picture ONLY if it's NOT in the default folder
                if (!string.IsNullOrEmpty(userProfile.ProfilePicturePath))
                {
                    var profilePicturePath = Path.Combine(_env.WebRootPath, userProfile.ProfilePicturePath.TrimStart('/'));
                    if (System.IO.File.Exists(profilePicturePath) && !profilePicturePath.StartsWith(defaultFolderPath))
                    {
                        System.IO.File.Delete(profilePicturePath);
                        Console.WriteLine($"Deleted profile picture: {profilePicturePath}");
                    }
                }

                // ✅ Delete background picture ONLY if it's NOT in the default folder
                if (!string.IsNullOrEmpty(userProfile.BackgroundImagePath))
                {
                    var backgroundImagePath = Path.Combine(_env.WebRootPath, userProfile.BackgroundImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(backgroundImagePath) && !backgroundImagePath.StartsWith(defaultFolderPath))
                    {
                        System.IO.File.Delete(backgroundImagePath);
                        Console.WriteLine($"Deleted background image: {backgroundImagePath}");
                    }
                }

                // ✅ Remove user’s likes
                if (userProfile.Likes != null && userProfile.Likes.Any())
                {
                    userProfile.Likes.Clear();
                }

                var userReports = await _context.CommentReports
                .Where(r => r.ReportedByUserId == userId)
                .ToListAsync();

                _context.CommentReports.RemoveRange(userReports);

                _context.UserProfiles.Remove(userProfile);
            }

            // 🔹 STEP 2: Delete the User’s Uploaded Content & Associated Files
            var userContent = await _context.UploadContents.Where(c => c.UserId == userId).ToListAsync();
            foreach (var content in userContent)
            {
                // ✅ Remove relations first
                var relatedAsMain = _context.UploadContentRelation
                    .Where(r => r.UploadContentId == content.Id);
                var relatedAsRelated = _context.UploadContentRelation
                    .Where(r => r.RelatedContentId == content.Id);

                _context.UploadContentRelation.RemoveRange(relatedAsMain);
                _context.UploadContentRelation.RemoveRange(relatedAsRelated);

                var mainFilePath = Path.Combine(_env.WebRootPath, content.ContentPath.TrimStart('/'));
                var thumbnailFilePath = !string.IsNullOrEmpty(content.ThumbnailPath)
                    ? Path.Combine(_env.WebRootPath, content.ThumbnailPath.TrimStart('/'))
                    : null;

                if (System.IO.File.Exists(mainFilePath))
                {
                    System.IO.File.Delete(mainFilePath);
                    Console.WriteLine($"Deleted content file: {mainFilePath}");
                }

                if (!string.IsNullOrEmpty(thumbnailFilePath) && System.IO.File.Exists(thumbnailFilePath))
                {
                    System.IO.File.Delete(thumbnailFilePath);
                    Console.WriteLine($"Deleted thumbnail file: {thumbnailFilePath}");
                }

                _context.UploadContents.Remove(content);
            }

            // 🔹 STEP 3: Delete All Comments Made by the User
            var userComments = await _context.Comments.Where(c => c.UserId == userId).ToListAsync();
            _context.Comments.RemoveRange(userComments);

            // 🔹 STEP 3.5: Delete All Forum Comments Made by the User
            var userForumComments = await _context.ForumComments
                .Where(fc => fc.CreatedByUserId == userId)
                .ToListAsync();

            _context.ForumComments.RemoveRange(userForumComments);

            // 🔹 STEP 4: Save Changes to Database BEFORE Deleting the User
            await _context.SaveChangesAsync();

            // 🔹 STEP 5: Delete User from Identity System
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error occurred deleting user.");
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User with ID '{UserId}' deleted themselves.", userId);

            return Redirect("~/");
        }
    }
}
