using BookKeepingWeb.Data;
using BookKeepingWeb.Helpers;
using BookKeepingWeb.Models;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using BookKeepingWeb.Controllers;
using Humanizer;
using Ganss.Xss;
using System.Text.RegularExpressions;


[Authorize]
public class ProfileController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly AccountValidationService _validationService;
    private readonly ApplicationDbContext _db;
    private static readonly string gif = ".GIF";
    private static readonly string[] image = { ".PNG", ".JPG", ".JPEG", ".WEBP" };
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWebHostEnvironment _env; // ✅ Added this for file deletion
    private readonly ILogger<AdminController> _logger;

    public ProfileController(SignInManager<IdentityUser> signInManager, AccountValidationService validationService, ApplicationDbContext db, UserManager<IdentityUser> userManager, IWebHostEnvironment env, ILogger<AdminController> logger)
    {
        _signInManager = signInManager;
        _validationService = validationService;
        _db = db;
        _userManager = userManager;
        _env = env; // ✅ Store reference to the hosting environment
        _logger = logger;
    }

    /// <summary>
    /// Display the index page for the user's profile
    /// </summary>
    /// <returns>a page</returns>

    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _userManager.GetUserId(User); // Get the logged-in user's ID

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Attempted to access Profile/Index without a valid user ID.");
                return RedirectToAction("Login", "Account"); // Or show a custom error view
            }

            var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                userProfile = new UserProfile { UserId = userId };

                try
                {
                    _db.UserProfiles.Add(userProfile);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Created new user profile for user ID: {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create a new profile for user ID: {UserId}", userId);
                    TempData["Error"] = "Something went wrong while setting up your profile. Please try again later.";
                    return RedirectToAction("Error", "Home");
                }
            }

            return View(userProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in Profile/Index.");
            TempData["Error"] = "Unexpected error occurred. Please try again later.";
            return RedirectToAction("Error", "Home");
        }
    }

    public FileType? GetFileType(string extension)
    {
        if (extension == gif)
        {
            return FileType.Gif;
        }
        else if (image.Contains(extension))
        {
            return FileType.Image;
        }

        return null;
    }

    /// <summary>
    /// Update the user's profile
    /// </summary>
    /// <param name="updatedProfile">the userprofile object</param>
    /// <param name="uploadedFile">the new profile picture</param>
    /// <param name="backgroundImage">the new backgroun picture</param>
    /// <returns></returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Index(UserProfile updatedProfile, IFormFile uploadedFile, IFormFile backgroundImage)
    {
        // validate if they're not a banned user or they exist in the db
        // this is important to have so that when a user is banned but they're technically logged in still because of 
        // cookies and how they work, this makes sure to validated when they try to upload or do anything stupid even after they're banned
        if (!await _validationService.IsUserStillValid(User))
        {
            await _signInManager.SignOutAsync();
            TempData["BanNotice"] = "Your account has been banned.";
            return RedirectToAction("Banned", "Home");
        }
        //////////////////////////////////////////////

        var userId = _userManager.GetUserId(User);
        var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);

        if (userProfile == null)
        {
            TempData["ProfileError"] = "Profile not found.";
            return RedirectToAction("Display");
        }

        try
        {
            //MUST Check if ScreenName is empty
            if (string.IsNullOrWhiteSpace(updatedProfile.ScreenName))
            {
                ModelState.AddModelError("ScreenName", "Screen Name cannot be empty.");
                return View(updatedProfile); // Return to the form with the error message
            }

            // ✅ Trim first
            updatedProfile.ScreenName = updatedProfile.ScreenName.Trim();

            // ✅ Encode input to prevent XSS
            updatedProfile.ScreenName = Regex.Replace(updatedProfile.ScreenName, @"[^\w\s]", "");
            updatedProfile.ScreenName = System.Net.WebUtility.HtmlEncode(updatedProfile.ScreenName);

            // ✅ Ensure it fits within the database limit (e.g., 20 characters)
            if (updatedProfile.ScreenName.Length > 20)
            {
                updatedProfile.ScreenName = updatedProfile.ScreenName.Substring(0, 20);
            }


            if (updatedProfile.Age < 18 || updatedProfile.Age > 100) // Adjust max as needed
            {
                ModelState.AddModelError("Age", "Age must be between 18 and 100.");
                return View(updatedProfile);
            }

            // Default to empty string if null (Prevents NullReferenceException)
            updatedProfile.Description = updatedProfile.Description ?? "";

            // ✅ Step 1: Trim spaces to avoid empty spaces being stored
            updatedProfile.Description = updatedProfile.Description.Trim();

            // Validate length
            if (updatedProfile.Description.Length > 500)
            {
                ModelState.AddModelError("Description", "Description cannot exceed 500 characters.");
                return View(updatedProfile);
            }

            // ✅ Step 3: Sanitize input (Removes harmful HTML while allowing safe elements)
            var sanitizer = new HtmlSanitizer();
            sanitizer.AllowedTags.Add("b");   // Allow <b> for bold text
            sanitizer.AllowedTags.Add("i");   // Allow <i> for italic text
            sanitizer.AllowedTags.Add("u");
            sanitizer.AllowedAttributes.Add("href");  // Allow 'href' attribute for <a>

            // Remove scripts but keep text content
            updatedProfile.Description = sanitizer.Sanitize(updatedProfile.Description);


            // ✅ Allow Country to be null but validate if it's provided
            if (!string.IsNullOrWhiteSpace(updatedProfile.Country))
            {
                // ✅ Validate max length (prevents excessively long inputs)
                if (updatedProfile.Country.Length > 56) // The longest country name is 56 chars
                {
                    ModelState.AddModelError("Country", "Invalid country selection.");
                    return View(updatedProfile);
                }

                // ✅ Sanitize input (Prevents XSS)
                updatedProfile.Country = System.Net.WebUtility.HtmlEncode(updatedProfile.Country);
            }

            // Update text fields
            // ✅ Store the encoded but truncated value
            userProfile.ScreenName = updatedProfile.ScreenName;
            userProfile.Description = updatedProfile.Description;
            userProfile.Country = updatedProfile.Country;
            userProfile.Age = updatedProfile.Age;

            // ✅ Allowed file extensions & MIME types
            var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png" };
            var allowedMimeTypes = new HashSet<string> { "image/jpeg", "image/png" };
            const int maxFileSize = 5 * 1024 * 1024; // 5MB

            if (uploadedFile != null && uploadedFile.Length > 0)
            {
                string defaultFolderPath = Path.Combine(_env.WebRootPath, "default");
                var contentType = uploadedFile.ContentType.ToLower();
                var extension = Path.GetExtension(uploadedFile.FileName).ToLower();

                // ✅ 2. Validate extension & MIME type
                if (!allowedExtensions.Contains(extension) || !allowedMimeTypes.Contains(contentType))
                {
                    ModelState.AddModelError("ProfilePicturePath", "Invalid file type. Only JPG, JPEG, and PNG are allowed.");
                    return View(updatedProfile);
                }

                // ✅ 3. Validate file size
                if (uploadedFile.Length > maxFileSize)
                {
                    ModelState.AddModelError("ProfilePicturePath", "File size must not exceed 5MB.");
                    return View(updatedProfile);
                }

                // ✅ 4. Validate image content (Prevents XSS)
                try
                {
                    using (var image = SixLabors.ImageSharp.Image.Load(uploadedFile.OpenReadStream()))
                    {
                        // Image is valid
                    }
                }
                catch
                {
                    ModelState.AddModelError("ProfilePicturePath", "Invalid image file.");
                    return View(updatedProfile);
                }

                // ✅ 5. Delete old profile picture (if exists)
                if (!string.IsNullOrEmpty(userProfile.ProfilePicturePath))
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath, userProfile.ProfilePicturePath.TrimStart('/'));
                    if (!oldFilePath.StartsWith(_env.WebRootPath))
                    {
                        _logger.LogWarning("Invalid profile picture path detected: {Path}", oldFilePath);
                    }
                    else if (System.IO.File.Exists(oldFilePath) && !oldFilePath.StartsWith(defaultFolderPath))
                    {
                        SafeDelete(oldFilePath, _logger);
                        _logger.LogInformation("Deleted old profile picture: {FilePath}", oldFilePath);
                    }
                }

                // ✅ 6. Secure File Storage
                string profilePictureFolder = Path.Combine(_env.WebRootPath, "uploads/profile_pictures");
                Directory.CreateDirectory(profilePictureFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + extension;
                string tempFilePath = Path.Combine(profilePictureFolder, uniqueFileName);

                // ✅ 7. Save the uploaded file
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await uploadedFile.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                // ✅ 7.5. Add a short delay to let the filesystem settle
                await Task.Delay(1000); // 100ms delay

                // ✅ 8. Process image
                string processedImagePath = ProcessImage(tempFilePath, 350f, 300f);
                if (processedImagePath != null)
                {
                    userProfile.ProfilePicturePath = processedImagePath;

                    // ✅ 8.5.Add a short delay to ensure file handles are released
                    await Task.Delay(1000); // 100ms delay

                    // ✅ 9. Delete temp file safely AFTER processing
                    SafeDelete(tempFilePath, _logger);
                }
                else
                {
                    _logger.LogError("Processing profile picture failed for: {FilePath}", tempFilePath);
                    ModelState.AddModelError("ProfilePicturePath", "Error processing image.");
                    return View(updatedProfile);
                }
            }

            // Check if a background file is uploaded
            if (backgroundImage != null)
            {
                string defaultFolderPath = Path.Combine(_env.WebRootPath, "default");
                var contentType = backgroundImage.ContentType.ToLower();
                var extension = Path.GetExtension(backgroundImage.FileName).ToLower();

                // ✅ Check if the file extension and MIME type are allowed
                if (!allowedExtensions.Contains(extension) || !allowedMimeTypes.Contains(contentType))
                {
                    ModelState.AddModelError("BackgroundImagePath", "Invalid file type. Only JPG, JPEG, and PNG are allowed.");
                    return View(updatedProfile);
                }

                // ✅ Validate file size
                if (backgroundImage.Length > maxFileSize)
                {
                    ModelState.AddModelError("BackgroundImagePath", "File size must not exceed 5MB.");
                    return View(updatedProfile);
                }

                // ✅ Validate image content (Prevents XSS)
                try
                {
                    using (var image = SixLabors.ImageSharp.Image.Load(backgroundImage.OpenReadStream()))
                    {
                        // Image is valid
                    }
                }
                catch
                {
                    ModelState.AddModelError("BackgroundImagePath", "Invalid image file.");
                    return View(updatedProfile);
                }

                // ✅ Delete the old background image only if it's not in the default folder
                if (!string.IsNullOrEmpty(userProfile.BackgroundImagePath))
                {
                    var oldBgFilePath = Path.Combine(_env.WebRootPath, userProfile.BackgroundImagePath.TrimStart('/'));
                    if (!oldBgFilePath.StartsWith(_env.WebRootPath))
                    {
                        _logger.LogWarning("Invalid background image path detected: {Path}", oldBgFilePath);
                    }
                    else if (System.IO.File.Exists(oldBgFilePath) && !oldBgFilePath.StartsWith(defaultFolderPath))
                    {
                        SafeDelete(oldBgFilePath, _logger);
                        _logger.LogInformation("Deleted old background image: {FilePath}", oldBgFilePath);
                    }
                }

                // ✅ Secure File Storage
                string profilePictureFolder = Path.Combine(_env.WebRootPath, "uploads/profile_pictures");
                Directory.CreateDirectory(profilePictureFolder); // Ensure folder exists

                string uniqueFileName = Guid.NewGuid().ToString() + extension;
                string tempFilePath = Path.Combine(profilePictureFolder, uniqueFileName);

                // ✅ Save the uploaded file with proper synchronization
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await backgroundImage.CopyToAsync(stream);
                    await stream.FlushAsync(); // Ensure the file is fully written to disk
                }

                // ✅ 7.5. Add a short delay to let the filesystem settle
                await Task.Delay(1000); // 100ms delay

                // ✅ Process image (no delay needed with proper file handling)
                string processedImagePath = ProcessImage(tempFilePath, 750f, 550f);
                if (processedImagePath != null) // Assuming ProcessImage returns null on failure
                {
                    userProfile.BackgroundImagePath = processedImagePath;

                    // ✅ 8.5. Add a short delay to ensure file handles are released
                    await Task.Delay(1000); // 100ms delay

                    // ✅ Delete temp file safely AFTER processing
                    SafeDelete(tempFilePath, _logger);
                }
                else
                {
                    _logger.LogError("Processing background image failed for: {FilePath}", tempFilePath);
                    ModelState.AddModelError("BackgroundImagePath", "Error processing image.");
                    return View(updatedProfile);
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update failed for user profile {UserId}", userId);
                ModelState.AddModelError("", "A database error occurred. Please try again later.");
                return View(updatedProfile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while saving profile for user {UserId}", userId);
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                return View(updatedProfile);
            }

            return RedirectToAction("Display"); // Redirect to the Display action
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during profile update for user {UserId}", userId);
            ModelState.AddModelError("", "Something went wrong while updating your profile. Please try again.");
            return View(updatedProfile);
        }

    }

    /// <summary>
    /// Display the user's content
    /// </summary>
    /// <param name="tab">the name of the tab</param>
    /// <param name="page">the page the user is on</param>
    /// <param name="pageSize">the number of posts per page</param>
    /// <returns></returns>
    public async Task<IActionResult> Display(string tab = "MyContent", int page = 1, int pageSize = 12)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Display called without a valid user ID.");
            return Unauthorized();
        }

        try
        {
            var userProfile = await _db.UserProfiles.FindAsync(userId);
            if (userProfile == null)
            {
                _logger.LogWarning("Display called but no profile found for user: {UserId}", userId);
                TempData["ProfileError"] = "Your profile could not be loaded.";
                return RedirectToAction("Index");
            }

            bool updated = false;

            if (string.IsNullOrWhiteSpace(userProfile.ProfilePicturePath))
            {
                userProfile.ProfilePicturePath = "/default/default-profile.png";
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(userProfile.BackgroundImagePath))
            {
                userProfile.BackgroundImagePath = "/default/default-wallpaper.png";
                updated = true;
            }

            if (updated)
            {
                _db.UserProfiles.Update(userProfile);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set default images for user profile: {UserId}", userId);
                    TempData["ProfileError"] = "Could not update your default images.";
                    // Continue rendering the view anyway
                }
            }

            IQueryable<UploadContent> contentQuery;

            if (tab == "Liked")
            {
                var likedContentIds = userProfile.Likes ?? new List<Guid>();
                contentQuery = _db.UploadContents
                    .Where(content => likedContentIds.Contains(content.Id))
                    .OrderByDescending(c => c.CreatedDateTime);
            }
            else
            {
                contentQuery = _db.UploadContents
                    .Where(content => content.UserId == userId)
                    .OrderByDescending(c => c.CreatedDateTime);
            }

            var paginatedContent = PaginatedList<UploadContent>.Create(contentQuery, page, pageSize);

            ViewData["UserContent"] = paginatedContent;
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = paginatedContent.TotalPages;
            ViewData["ActiveTab"] = tab;

            return View(userProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while loading profile display for user: {UserId}", userId);
            TempData["DisplayError"] = "Something went wrong while loading your profile.";
            return RedirectToAction("Index");
        }
    }


    // Helper method
    private void SafeDelete(string filePath, ILogger logger)
    {
        const int maxRetries = 3;
        const int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                return;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1)
                {
                    logger.LogError(ex, "Failed to delete file after retries: {FilePath}", filePath);
                }
                Thread.Sleep(delayMs);
            }
        }
    }

    /// <summary>
    /// Display the user's public profile
    /// </summary>
    /// <param name="userId">user id of the profile page to view</param>
    /// <param name="page">the page of content the user is on</param>
    /// <param name="pageSize">the number of posts per page</param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> PublicProfile(string userId, int page = 1, int pageSize = 12)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("PublicProfile called with null or empty userId.");
            return BadRequest("Invalid user ID.");
        }

        try
        {
            // Redirect if user is trying to view their own public profile
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUserId = _userManager.GetUserId(User);
                if (currentUserId == userId)
                {
                    _logger.LogInformation("User {UserId} redirected to Display instead of PublicProfile.", userId);
                    return RedirectToAction("Display", "Profile");
                }
            }

            // Fetch the user profile
            var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (userProfile == null)
            {
                _logger.LogWarning("PublicProfile: No profile found for userId {UserId}", userId);
                return NotFound();
            }

            // Fetch content
            var userContentQuery = _db.UploadContents
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedDateTime);

            var paginatedContent = PaginatedList<UploadContent>.Create(userContentQuery, page, pageSize);

            ViewData["UserContent"] = paginatedContent;
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = paginatedContent.TotalPages;

            return View(userProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading public profile for userId: {UserId}", userId);
            TempData["ProfileError"] = "An error occurred while loading the profile. Please try again later.";
            return RedirectToAction("Index", "Home");
        }
    }


    private string ProcessGif(string inputPath, float width, float height)
    {
        string uploadsFolder = Path.Combine("wwwroot", "images");
        Directory.CreateDirectory(uploadsFolder); // Ensure directory exists

        string thumbnailFileName = $"{Guid.NewGuid()}.gif"; // Generate unique name
        string thumbnailPath = Path.Combine(uploadsFolder, thumbnailFileName);

        using (var collection = new MagickImageCollection())
        {
            // Read GIF safely with FileStream to prevent file locks
            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                collection.Read(fs);
            }

            collection.Coalesce(); // Ensure proper frame merging to maintain transparency

            // Create an animated GIF thumbnail (250x200)
            using (var thumbnailCollection = new MagickImageCollection())
            {
                foreach (var frame in collection) // 🔥 Keep all frames for animation
                {
                    var resizedFrame = new MagickImage(frame);
                    resizedFrame.FilterType = FilterType.Lanczos;
                    resizedFrame.Resize(new MagickGeometry(350, 300) { IgnoreAspectRatio = false });
                    resizedFrame.BackgroundColor = MagickColors.Transparent;

                    thumbnailCollection.Add(resizedFrame);
                }

                // Preserve original animation properties
                thumbnailCollection[0].AnimationIterations = 0; // Infinite loop

                // Save the animated GIF thumbnail
                using (var msThumb = new MemoryStream())
                {
                    thumbnailCollection.Write(msThumb, MagickFormat.Gif);
                    System.IO.File.WriteAllBytes(thumbnailPath, msThumb.ToArray());
                }
            }
        }

        return $"/images/{thumbnailFileName}"; // Return relative path for web use
    }

    private string? ProcessImage(string inputPath, float maxWidth, float maxHeight)
    {
        try
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "images");
            Directory.CreateDirectory(uploadsFolder);

            string thumbnailFileName = $"{Guid.NewGuid()}.webp";
            string thumbnailPath = Path.Combine(uploadsFolder, thumbnailFileName);

            using var stream = System.IO.File.OpenRead(inputPath);
            using var image = SixLabors.ImageSharp.Image.Load(stream);

            // Resize if needed
            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                image.Mutate(x =>
                {
                    x.AutoOrient();
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size((int)maxWidth, (int)maxHeight),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Bicubic // Faster alternative to Lanczos3
                    });
                });
            }
            else
            {
                image.Mutate(x => x.AutoOrient()); // Apply AutoOrient anyway
            }

            image.Metadata.ExifProfile = null; // Remove EXIF metadata to reduce file size

            image.Save(thumbnailPath, new WebpEncoder
            {
                Quality = 90, // 90 is visually perfect and smaller
                Method = WebpEncodingMethod.Fastest
            });

            return $"/images/{thumbnailFileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image: {InputPath}", inputPath);
            return null;
        }
    }






}