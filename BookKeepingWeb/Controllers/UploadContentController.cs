using BookKeepingWeb.Data;
using BookKeepingWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.IO;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using static System.Net.Mime.MediaTypeNames;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Xabe.FFmpeg;
using System.Threading.Tasks;
using ImageMagick;
using Ganss.Xss;
using BookKeepingWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.VisualBasic.ApplicationServices;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.RegularExpressions;


namespace BookKeepingWeb.Controllers
{
    [Authorize]
    public class UploadContentController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HomeController> _logger;
        private static readonly string gif = ".GIF";
        private static readonly string[] video = {".MP4", ".MOV", ".AVI", ".WMV" };
        private static readonly string[] image = { ".PNG", ".JPG", ".JPEG", ".WEBP" };
        private static readonly string uploadsFolder = Path.Combine("wwwroot", "uploads");
        private readonly AccountValidationService _validationService;
        private readonly IWebHostEnvironment _env;


        public UploadContentController(SignInManager<IdentityUser> signInManager,AccountValidationService validationService, ILogger<HomeController> logger, ApplicationDbContext db, IWebHostEnvironment env)
        {
            _signInManager = signInManager;
            _validationService = validationService;
            _logger = logger;
            _db = db;
            _env = env;
        }

        /// <summary>
        /// Display's the create content page
        /// </summary>
        /// <returns>A view</returns>
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userContent = await _db.UploadContents.Where(c => c.UserId == userId).ToListAsync();
            return View(userContent);
        }

        /// <summary>
        /// Display the create content page
        /// </summary>
        /// <returns>A view</returns>
        public async Task<IActionResult> Create()
        {
            // Get the current user's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Retrieve the user profile from the database
            var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

            return View();
        }

        public async Task<IActionResult> AdminUpload()
        {
            // Get the current user's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Retrieve the user profile from the database
            var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

            return View();
        }

        /// <summary>
        /// Get the file type given an extension
        /// </summary>
        /// <param name="extension">the file extension</param>
        /// <returns>The filetype if exists, or null if not exists</returns>
        public FileType? GetFileType(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return null;

            extension = extension.Trim().ToUpperInvariant();

            if (extension == gif)
            {
                return FileType.Gif;
            }
            else if (video.Contains(extension))
            {
                return FileType.Video;
            }
            else if (image.Contains(extension))
            {
                return FileType.Image;
            }

            return null;
        }

        /// <summary>
        /// Get the list of tags from a string
        /// </summary>
        /// <param name="tagdata">string of tags</param>
        /// <returns>a list of tags or an empty list if the string is in the incorrect folder</returns>
        public async Task<List<Tag>> GetTagData(string tagdata)
        {
            var tagList = new List<Tag>();
            List<string> tags = new();

            if (!string.IsNullOrWhiteSpace(tagdata))
            {
                try
                {
                    var tagDataObject = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(tagdata);

                    if (tagDataObject != null && tagDataObject.TryGetValue("userDefinedTags", out var userTags))
                    {
                        tags = userTags;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse user-defined tag data: {TagData}", tagdata);
                    return tagList; // Return empty safely
                }
            }

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;

                try
                {
                    var trimmed = tag.Trim();
                    var sanitized = System.Net.WebUtility.HtmlEncode(trimmed);

                    if (sanitized.Length > 30)
                        sanitized = sanitized.Substring(0, 30);

                    var existing = await _db.Tags
                        .SingleOrDefaultAsync(t => t.Name.ToLower() == sanitized.ToLower());

                    if (existing != null)
                    {
                        existing.Count++;
                        tagList.Add(existing);
                        continue;
                    }

                    var newTag = new Tag
                    {
                        TagId = Guid.NewGuid(),
                        Name = sanitized,
                        Count = 1
                    };

                    await _db.Tags.AddAsync(newTag);
                    tagList.Add(newTag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing tag: {Tag}", tag);
                    // Optionally skip and continue to next tag instead of halting
                    continue;
                }
            }

            return tagList;
        }



        /// <summary>
        /// create a post with only one media
        /// </summary>
        /// <param name="obj">uploaded content object</param>
        /// <param name="uploadedFile">a single uploaded content</param>
        /// <param name="tagdata">string of tagdata</param>
        /// <returns></returns>
        /// <exception cref="Exception">idk</exception>
        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)] // 200 MB
        [RequestSizeLimit(209715200)] // 200 MB
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AdminUpload(UploadContent obj, List<IFormFile> uploadedFiles, string tagdata, List<string> PredefinedTags)
        {
            //validate if they're not a banned user or they exist in the db
            if (!await _validationService.IsUserStillValid(User))
            {
                await _signInManager.SignOutAsync();
                TempData["BanNotice"] = "Your account has been banned.";
                return RedirectToAction("Banned", "Home");
            }

            // Assign UserId programmatically
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("UserId is null. Ensure the user is logged in.");
            }

            if (uploadedFiles.Count == 0)
            {
                ModelState.AddModelError("ContentPath", "Please upload a file.");
                return View(obj);
            }

            if (uploadedFiles.Count > 1)
            {
                ModelState.AddModelError("ContentPath", "Only one file can be uploaded at a time.");
                return View(obj);
            }

            // Track final saved file paths for cleanup on error
            string? finalMainPath = null;
            string? finalThumbnailPath = null;

            try
            {
                IFormFile uploadedFile = uploadedFiles[0];

                //  Define allowed extensions
                var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".mp4" };

                var extensionX = Path.GetExtension(uploadedFile.FileName).ToLower(); // Get the file extension

                //  Check if the file extension is NOT in the allowed list
                if (!allowedExtensions.Contains(extensionX))
                {
                    ModelState.AddModelError("ContentPath", "Invalid file type. Only JPG, JPEG, PNG, and MP4 are allowed.");
                    return View(obj);
                }

                // ✅ Get MIME type from Content-Type header
                var contentType = uploadedFile.ContentType.ToLower();

                // ✅ Define allowed MIME types
                var allowedMimeTypes = new HashSet<string>
                {
                    "image/jpeg", "image/png", "video/mp4"
                };

                // ✅ Validate MIME type
                if (!allowedMimeTypes.Contains(contentType))
                {
                    ModelState.AddModelError("ContentPath", "Invalid file content. Only JPG, PNG, and MP4 are allowed.");
                    return View(obj);
                }

                // ✅ Validate file content if it's an image
                if (contentType.StartsWith("image/"))
                {
                    try
                    {
                        using (var image = SixLabors.ImageSharp.Image.Load(uploadedFile.OpenReadStream()))
                        {
                            // Image successfully loaded, so it's valid
                        }
                    }
                    catch
                    {
                        ModelState.AddModelError("ContentPath", "The uploaded image file is invalid or corrupted.");
                        return View(obj);
                    }
                }


                // Handle user-defined and predefined tags safely
                var tagDataObject = new Dictionary<string, List<string>>();
                if (!string.IsNullOrEmpty(tagdata))
                {
                    try
                    {
                        tagDataObject = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(tagdata) ?? new Dictionary<string, List<string>>();
                    }
                    catch (JsonException)
                    {
                        ModelState.AddModelError("Tags", "Invalid tag data format.");
                        return View(obj);
                    }
                }

                // Extract predefined and user-defined tags
                List<string> predefinedTags = tagDataObject.ContainsKey("predefinedTags") ? tagDataObject["predefinedTags"] : new List<string>();
                List<string> userDefinedTags = tagDataObject.ContainsKey("userDefinedTags") ? tagDataObject["userDefinedTags"] : new List<string>();

                // Ensure at least one predefined tag is selected
                if (predefinedTags.Count == 0)
                {
                    ModelState.AddModelError("Tags", "Please select at least one predefined tag.");
                    return View(obj);
                }

                // Combine predefined and user-defined tags
                List<string> tags = predefinedTags.Concat(userDefinedTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Validate tags exist
                if (tags.Count == 0)
                {
                    ModelState.AddModelError("Tags", "Please add at least one tag.");
                    return View(obj);
                }
                // Process tags
                List<Tag> tagList = new List<Tag>();
                if (tags != null && tags.Count > 0)
                {
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;

                        // ✅ Sanitize first
                        var sanitizedTag = System.Net.WebUtility.HtmlEncode(tag);

                        // ✅ THEN truncate the sanitized version
                        sanitizedTag = sanitizedTag.Length > 30 ? sanitizedTag.Substring(0, 30) : sanitizedTag;


                        var existingTag = await _db.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == sanitizedTag.ToLower());
                        if (existingTag != null)
                        {
                            existingTag.Count++; // Increment existing tag count
                            tagList.Add(existingTag);
                            continue;
                        }

                        var newTag = new Tag
                        {
                            TagId = Guid.NewGuid(),
                            Count = 1,
                            Name = sanitizedTag
                        };

                        await _db.Tags.AddAsync(newTag);
                        tagList.Add(newTag);
                    }
                }
                else
                {
                    ModelState.AddModelError("Tags", "Please add at least one tag");
                    return View(obj);
                }

                obj.UserId = userId;
                obj.Tags = tagList;

                // Ensure title is not null
                obj.Title = obj.Title ?? "";

                // Trim and validate
                obj.Title = obj.Title.Trim();

                if (obj.Title.Length > 90)
                {
                    ModelState.AddModelError("Title", "Title cannot exceed 90 characters.");
                    return View(obj);
                }

                // Sanitize title (HTML encoding to prevent XSS)
                var sanitizer1 = new HtmlSanitizer();
                sanitizer1.AllowedTags.Clear();        // No HTML tags allowed
                sanitizer1.AllowedAttributes.Clear();  // No attributes allowed either

                obj.Title = sanitizer1.Sanitize(obj.Title); // This removes <script>, <img>, etc

                // If after sanitization the title is empty, generate a safe fallback
                if (string.IsNullOrWhiteSpace(obj.Title))
                {
                    obj.Title = "Renamed Title " + GenerateRandomSuffix();
                }

                // ✅ Limit to 90 characters
                if (obj.Title.Length > 90)
                {
                    obj.Title = obj.Title.Substring(0, 90);
                }

                // ✅ Ensure Description is not null before saving
                obj.Description = obj.Description ?? "";

                // Trim and validate
                obj.Description = obj.Description.Trim();

                if (obj.Description.Length > 490)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 490 characters.");
                    return View(obj);
                }

                var sanitizer2 = new HtmlSanitizer();
                sanitizer2.AllowedTags.Clear(); // Start clean

                // ✅ Only allow safe formatting tags (no links)
                sanitizer2.AllowedTags.Add("b");
                sanitizer2.AllowedTags.Add("i");
                sanitizer2.AllowedTags.Add("u");

                // ✅ Make sure no hrefs sneak in
                sanitizer2.AllowedAttributes.Clear();
                sanitizer2.AllowedSchemes.Clear();

                // ✅ Prevent script/image-based XSS
                sanitizer2.KeepChildNodes = false;

                // ✅ Sanitize
                obj.Description = sanitizer2.Sanitize(obj.Description);


                // Validate the ModelState after assigning UserId
                if (!ModelState.IsValid)
                {
                    // Log ModelState errors for debugging
                    foreach (var entry in ModelState)
                    {
                        if (entry.Value.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                        {
                            Console.WriteLine($"Field: {entry.Key}");
                            foreach (var error in entry.Value.Errors)
                            {
                                Console.WriteLine($"Error: {error.ErrorMessage}");
                            }
                        }
                    }

                    return View(obj); // Return view with errors
                }

                var extension = Path.GetExtension(uploadedFile.FileName).ToUpper();
                FileType? type = GetFileType(extension);

                if (type == null)
                {
                    ModelState.AddModelError("ContentPath", "Please Upload an image or video");
                    return View(obj);
                }

                obj.ContentType = (FileType)type;

                // Handle image upload
                if (uploadedFile != null)
                {
                    if (uploadedFile.Length > 209715200) // Validate file size
                    {
                        ModelState.AddModelError("ContentPath", "The uploaded file exceeds the maximum allowed size of 200 MB.");
                        return View(obj);
                    }

                    // Define file paths
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    var mainImagesFolder = Path.Combine(uploadsFolder, "main");
                    var thumbnailsFolder = Path.Combine(uploadsFolder, "thumbnails");

                    // Add temp folder
                    var tempFolder = Path.Combine(_env.ContentRootPath, "Temp"); // Outside wwwroot
                    Directory.CreateDirectory(mainImagesFolder);
                    Directory.CreateDirectory(thumbnailsFolder);
                    Directory.CreateDirectory(tempFolder); // Ensure Temp exists

                    var fileExtension = Path.GetExtension(uploadedFile.FileName).ToLower();
                    var fileName = Guid.NewGuid().ToString();
                    var tempOriginalFilePath = Path.Combine(tempFolder, fileName + fileExtension); // Changed from Path.GetTempPath()
                    var mainFilePath = Path.Combine(mainImagesFolder, fileName + fileExtension);
                    var thumbnailFilePath = Path.Combine(thumbnailsFolder, fileName + ".webp");

                    // ✅ Assign cleanup targets before anything can fail
                    finalMainPath = mainFilePath;
                    finalThumbnailPath = thumbnailFilePath;

                    // Save the uploaded file temporarily
                    using (var stream = new FileStream(tempOriginalFilePath, FileMode.Create))
                    {
                        await uploadedFile.CopyToAsync(stream);
                    }

                    switch (type)
                    {
                        case FileType.Image:
                            ProcessImage(tempOriginalFilePath, mainFilePath, thumbnailFilePath);
                            break;

                        case FileType.Gif:
                            ProcessGif(tempOriginalFilePath, mainFilePath, thumbnailFilePath);
                            break;

                        case FileType.Video:
                            await ProcessVideo(uploadedFile, mainFilePath);
                            var thumbnailPath = Path.Combine(thumbnailsFolder, fileName + ".webp");
                            await ExtractVideoThumbnail(mainFilePath, thumbnailPath);
                            break;

                        default:
                            ModelState.AddModelError("ContentPath", "Unsupported file type.");
                            return View(obj);
                    }

                    // Delete the temporary file
                    System.IO.File.Delete(tempOriginalFilePath);

                    obj.ContentPath = "/uploads/main/" + fileName + fileExtension;
                    obj.ThumbnailPath = "/uploads/thumbnails/" + fileName + ".webp";


                }
                else
                {
                    ModelState.AddModelError("ContentPath", "Please upload a file.");
                    return View(obj);
                }

                // ✅ Generate and assign unique slug after file handling
                obj.Slug = await GenerateUniqueSlug(obj.Title);

                // Save to database
                await _db.UploadContents.AddAsync(obj);
                await _db.SaveChangesAsync();

                // Fetch up to 16 related contents by matching tags (excluding the current one)
                var related = await _db.UploadContents
                    .Include(uc => uc.Tags)
                    .Where(uc => uc.Id != obj.Id && uc.Tags.Any(tag => obj.Tags.Select(t => t.Name).Contains(tag.Name)))
                    .OrderByDescending(uc => uc.Tags.Count(tag => obj.Tags.Select(t => t.Name).Contains(tag.Name)))
                    .ThenByDescending(uc => uc.CreatedDateTime)
                    .Take(16)
                    .ToListAsync();

                // Create UploadContentRelation entries
                var relations = related.Select(r => new UploadContentRelation
                {
                    UploadContentId = obj.Id,
                    RelatedContentId = r.Id
                }).ToList();

                // Save relations
                await _db.AddRangeAsync(relations);
                await _db.SaveChangesAsync();

                return RedirectToAction("Display", "Profile");
            }
            catch (Exception ex)
            {
                // Log the unexpected error
                _logger.LogError(ex, "Error during content upload by user {UserId}", userId);

                // Clean up leftover files if something failed
                if (!string.IsNullOrEmpty(finalMainPath) && System.IO.File.Exists(finalMainPath))
                {
                    try { System.IO.File.Delete(finalMainPath); } catch { /* Optional: log this too */ }
                }

                if (!string.IsNullOrEmpty(finalThumbnailPath) && System.IO.File.Exists(finalThumbnailPath))
                {
                    try { System.IO.File.Delete(finalThumbnailPath); } catch { /* Optional: log this too */ }
                }

                // Add generic error to the form
                ModelState.AddModelError("", "Something went wrong while processing your upload. Please try again later.");

                return View(obj);
            }
        }

        private async Task<string> GenerateUniqueSlug(string title)
        {
            string baseSlug = title.ToLower().Trim();

            baseSlug = Regex.Replace(baseSlug, @"[^a-z0-9\s-]", "");
            baseSlug = Regex.Replace(baseSlug, @"\s+", "-");
            baseSlug = baseSlug.Trim('-');

            // Fallback if slug is empty
            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                baseSlug = "content";
            }

            // Optional: Limit slug length
            baseSlug = baseSlug.Length > 90 ? baseSlug.Substring(0, 90) : baseSlug;

            string slug = baseSlug;
            int counter = 1;

            while (await _db.UploadContents.AnyAsync(c => c.Slug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            return slug;
        }


        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)] // 200 MB
        [RequestSizeLimit(209715200)] // 200 MB
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UploadContent obj, List<IFormFile> uploadedFiles, string tagdata, List<string> PredefinedTags)
        {
            //validate if they're not a banned user or they exist in the db
            if (!await _validationService.IsUserStillValid(User))
            {
                await _signInManager.SignOutAsync();
                TempData["BanNotice"] = "Your account has been banned.";
                return RedirectToAction("Banned", "Home");
            }

            // Assign UserId programmatically
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new Exception("UserId is null. Ensure the user is logged in.");
            }

            if (uploadedFiles.Count == 0)
            {
                ModelState.AddModelError("ContentPath", "Please upload a file.");
                return View(obj);
            }

            if (uploadedFiles.Count > 1)
            {
                ModelState.AddModelError("ContentPath", "Only one file can be uploaded at a time.");
                return View(obj);
            }

            // Track final saved file paths for cleanup on error
            string? finalMainPath = null;
            string? finalThumbnailPath = null;

            try
            {
                IFormFile uploadedFile = uploadedFiles[0];

                //  Define allowed extensions
                var allowedExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".mp4" };

                var extensionX = Path.GetExtension(uploadedFile.FileName).ToLower(); // Get the file extension

                //  Check if the file extension is NOT in the allowed list
                if (!allowedExtensions.Contains(extensionX))
                {
                    ModelState.AddModelError("ContentPath", "Invalid file type. Only JPG, JPEG, PNG, and MP4 are allowed.");
                    return View(obj);
                }

                // ✅ Get MIME type from Content-Type header
                var contentType = uploadedFile.ContentType.ToLower();

                // ✅ Define allowed MIME types
                var allowedMimeTypes = new HashSet<string>
                {
                    "image/jpeg", "image/png", "video/mp4"
                };

                // ✅ Validate MIME type
                if (!allowedMimeTypes.Contains(contentType))
                {
                    ModelState.AddModelError("ContentPath", "Invalid file content. Only JPG, PNG, and MP4 are allowed.");
                    return View(obj);
                }

                // ✅ Validate file content if it's an image
                if (contentType.StartsWith("image/"))
                {
                    try
                    {
                        using (var image = SixLabors.ImageSharp.Image.Load(uploadedFile.OpenReadStream()))
                        {
                            // Image successfully loaded, so it's valid
                        }
                    }
                    catch
                    {
                        ModelState.AddModelError("ContentPath", "The uploaded image file is invalid or corrupted.");
                        return View(obj);
                    }
                }


                // Handle user-defined and predefined tags safely
                var tagDataObject = new Dictionary<string, List<string>>();
                if (!string.IsNullOrEmpty(tagdata))
                {
                    try
                    {
                        tagDataObject = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(tagdata) ?? new Dictionary<string, List<string>>();
                    }
                    catch (JsonException)
                    {
                        ModelState.AddModelError("Tags", "Invalid tag data format.");
                        return View(obj);
                    }
                }

                // Extract predefined and user-defined tags
                List<string> predefinedTags = tagDataObject.ContainsKey("predefinedTags") ? tagDataObject["predefinedTags"] : new List<string>();
                List<string> userDefinedTags = tagDataObject.ContainsKey("userDefinedTags") ? tagDataObject["userDefinedTags"] : new List<string>();

                // Ensure at least one predefined tag is selected
                if (predefinedTags.Count == 0)
                {
                    ModelState.AddModelError("Tags", "Please select at least one predefined tag.");
                    return View(obj);
                }

                // Combine predefined and user-defined tags
                List<string> tags = predefinedTags.Concat(userDefinedTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Validate tags exist
                if (tags.Count == 0)
                {
                    ModelState.AddModelError("Tags", "Please add at least one tag.");
                    return View(obj);
                }
                // Process tags
                List<Tag> tagList = new List<Tag>();
                if (tags != null && tags.Count > 0)
                {
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;

                        // ✅ Sanitize first
                        var sanitizedTag = System.Net.WebUtility.HtmlEncode(tag);

                        // ✅ THEN truncate the sanitized version
                        sanitizedTag = sanitizedTag.Length > 30 ? sanitizedTag.Substring(0, 30) : sanitizedTag;


                        var existingTag = await _db.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == sanitizedTag.ToLower());
                        if (existingTag != null)
                        {
                            existingTag.Count++; // Increment existing tag count
                            tagList.Add(existingTag);
                            continue;
                        }

                        var newTag = new Tag
                        {
                            TagId = Guid.NewGuid(),
                            Count = 1,
                            Name = sanitizedTag
                        };

                        await _db.Tags.AddAsync(newTag);
                        tagList.Add(newTag);
                    }
                }
                else
                {
                    ModelState.AddModelError("Tags", "Please add at least one tag");
                    return View(obj);
                }

                obj.UserId = userId;
                obj.Tags = tagList;

                // Ensure title is not null
                obj.Title = obj.Title ?? "";

                // Trim and validate
                obj.Title = obj.Title.Trim();

                if (obj.Title.Length > 90)
                {
                    ModelState.AddModelError("Title", "Title cannot exceed 90 characters.");
                    return View(obj);
                }

                // Sanitize title (HTML encoding to prevent XSS)
                var sanitizer1 = new HtmlSanitizer();
                sanitizer1.AllowedTags.Clear();        // No HTML tags allowed
                sanitizer1.AllowedAttributes.Clear();  // No attributes allowed either

                obj.Title = sanitizer1.Sanitize(obj.Title); // This removes <script>, <img>, etc

                // If after sanitization the title is empty, generate a safe fallback
                if (string.IsNullOrWhiteSpace(obj.Title))
                {
                    obj.Title = "Renamed Title " + GenerateRandomSuffix();
                }

                // ✅ Limit to 90 characters
                if (obj.Title.Length > 90)
                {
                    obj.Title = obj.Title.Substring(0, 90);
                }

                // ✅ Ensure Description is not null before saving
                obj.Description = obj.Description ?? "";

                // Trim and validate
                obj.Description = obj.Description.Trim();

                if (obj.Description.Length > 490)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 490 characters.");
                    return View(obj);
                }

                var sanitizer2 = new HtmlSanitizer();
                sanitizer2.AllowedTags.Clear(); // Start clean

                // ✅ Only allow safe formatting tags (no links)
                sanitizer2.AllowedTags.Add("b");
                sanitizer2.AllowedTags.Add("i");
                sanitizer2.AllowedTags.Add("u");

                // ✅ Make sure no hrefs sneak in
                sanitizer2.AllowedAttributes.Clear();
                sanitizer2.AllowedSchemes.Clear();

                // ✅ Prevent script/image-based XSS
                sanitizer2.KeepChildNodes = false;

                // ✅ Sanitize
                obj.Description = sanitizer2.Sanitize(obj.Description);


                // Validate the ModelState after assigning UserId
                if (!ModelState.IsValid)
                {
                    // Log ModelState errors for debugging
                    foreach (var entry in ModelState)
                    {
                        if (entry.Value.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                        {
                            Console.WriteLine($"Field: {entry.Key}");
                            foreach (var error in entry.Value.Errors)
                            {
                                Console.WriteLine($"Error: {error.ErrorMessage}");
                            }
                        }
                    }

                    return View(obj); // Return view with errors
                }

                var extension = Path.GetExtension(uploadedFile.FileName).ToUpper();
                FileType? type = GetFileType(extension);

                if (type == null)
                {
                    ModelState.AddModelError("ContentPath", "Please Upload an image or video");
                    return View(obj);
                }

                obj.ContentType = (FileType)type;

                // Handle image upload
                if (uploadedFile != null)
                {
                    if (uploadedFile.Length > 209715200) // Validate file size
                    {
                        ModelState.AddModelError("ContentPath", "The uploaded file exceeds the maximum allowed size of 200 MB.");
                        return View(obj);
                    }

                    // Define file paths
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    var mainImagesFolder = Path.Combine(uploadsFolder, "main");
                    var thumbnailsFolder = Path.Combine(uploadsFolder, "thumbnails");

                    // Add temp folder
                    var tempFolder = Path.Combine(_env.ContentRootPath, "Temp"); // Outside wwwroot
                    Directory.CreateDirectory(mainImagesFolder);
                    Directory.CreateDirectory(thumbnailsFolder);
                    Directory.CreateDirectory(tempFolder); // Ensure Temp exists

                    var fileExtension = Path.GetExtension(uploadedFile.FileName).ToLower();
                    var fileName = Guid.NewGuid().ToString();
                    var tempOriginalFilePath = Path.Combine(tempFolder, fileName + fileExtension); // Changed from Path.GetTempPath()
                    var mainFilePath = Path.Combine(mainImagesFolder, fileName + fileExtension);
                    var thumbnailFilePath = Path.Combine(thumbnailsFolder, fileName + ".webp");

                    // ✅ Assign cleanup targets before anything can fail
                    finalMainPath = mainFilePath;
                    finalThumbnailPath = thumbnailFilePath;

                    // Save the uploaded file temporarily
                    using (var stream = new FileStream(tempOriginalFilePath, FileMode.Create))
                    {
                        await uploadedFile.CopyToAsync(stream);
                    }

                    switch (type)
                    {
                        case FileType.Image:
                            ProcessImage(tempOriginalFilePath, mainFilePath, thumbnailFilePath);
                            break;

                        case FileType.Gif:
                            ProcessGif(tempOriginalFilePath, mainFilePath, thumbnailFilePath);
                            break;

                        case FileType.Video:
                            await ProcessVideo(uploadedFile, mainFilePath);
                            var thumbnailPath = Path.Combine(thumbnailsFolder, fileName + ".webp");
                            await ExtractVideoThumbnail(mainFilePath, thumbnailPath);
                            break;

                        default:
                            ModelState.AddModelError("ContentPath", "Unsupported file type.");
                            return View(obj);
                    }

                    // Delete the temporary file
                    System.IO.File.Delete(tempOriginalFilePath);

                    obj.ContentPath = "/uploads/main/" + fileName + fileExtension;
                    obj.ThumbnailPath = "/uploads/thumbnails/" + fileName + ".webp";

                    
                }
                else
                {
                    ModelState.AddModelError("ContentPath", "Please upload a file.");
                    return View(obj);
                }

                // ✅ Generate and assign unique slug after file handling
                obj.Slug = await GenerateUniqueSlug(obj.Title);

                // Save to database
                await _db.UploadContents.AddAsync(obj);
                await _db.SaveChangesAsync();

                // Fetch up to 16 related contents by matching tags (excluding the current one)
                var related = await _db.UploadContents
                    .Include(uc => uc.Tags)
                    .Where(uc => uc.Id != obj.Id && uc.Tags.Any(tag => obj.Tags.Select(t => t.Name).Contains(tag.Name)))
                    .OrderByDescending(uc => uc.Tags.Count(tag => obj.Tags.Select(t => t.Name).Contains(tag.Name)))
                    .ThenByDescending(uc => uc.CreatedDateTime)
                    .Take(16)
                    .ToListAsync();

                // Create UploadContentRelation entries
                var relations = related.Select(r => new UploadContentRelation
                {
                    UploadContentId = obj.Id,
                    RelatedContentId = r.Id
                }).ToList();

                // Save relations
                await _db.AddRangeAsync(relations);
                await _db.SaveChangesAsync();

                return RedirectToAction("Display", "Profile");
            }
            catch (Exception ex)
            {
                // Log the unexpected error
                _logger.LogError(ex, "Error during content upload by user {UserId}", userId);

                // Clean up leftover files if something failed
                if (!string.IsNullOrEmpty(finalMainPath) && System.IO.File.Exists(finalMainPath))
                {
                    try { System.IO.File.Delete(finalMainPath); } catch { /* Optional: log this too */ }
                }

                if (!string.IsNullOrEmpty(finalThumbnailPath) && System.IO.File.Exists(finalThumbnailPath))
                {
                    try { System.IO.File.Delete(finalThumbnailPath); } catch { /* Optional: log this too */ }
                }

                // Add generic error to the form
                ModelState.AddModelError("", "Something went wrong while processing your upload. Please try again later.");

                return View(obj);
            }
        }

        /// makes a random generated title 
        ///if after sanitization, it becomes empty
        private string GenerateRandomSuffix()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        /// Display the edit content page
        /// </summary>
        /// <param name="id">the id of the uploadedcontent</param>
        /// <returns>the edit page</returns>
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (!id.HasValue)
            {
                _logger.LogWarning("Edit called with null ID.");
                return BadRequest("Invalid request: ID is required.");
            }

            var contentFromDb = await _db.UploadContents
                .Include(i => i.Tags)
                .FirstOrDefaultAsync(i => i.Id == id.Value);

            if (contentFromDb == null)
            {
                _logger.LogWarning("Edit called with non-existent ID: {Id}", id);
                return NotFound();
            }

            return View(contentFromDb);
        }

        /// <summary>
        /// Display the edit page for editing the object
        /// </summary>
        /// <param name="obj">The uploadContent object</param>
        /// <param name="tagdata">String containing tag data</param>
        /// <returns>a page</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> Edit(UploadContent obj, string tagdata, List<string> predefinedTags)
        {
            //validate if they're not a banned user or they exist in the db
            if (!await _validationService.IsUserStillValid(User))
            {
                await _signInManager.SignOutAsync();
                TempData["BanNotice"] = "Your account has been banned.";
                return RedirectToAction("Banned", "Home");
            }

            var contentFromDb = await _db.UploadContents.Include(i => i.Tags).FirstOrDefaultAsync(i => i.Id == obj.Id);
            if (contentFromDb == null)
            {
                return NotFound();
            }

            try
            {
                // ✅ Title
                obj.Title = obj.Title?.Trim() ?? "";

                if (obj.Title.Length > 90)
                {
                    ModelState.AddModelError("Title", "Title cannot exceed 90 characters.");
                }
                // Sanitize title (HTML encoding to prevent XSS)
                var sanitizer3 = new HtmlSanitizer();
                sanitizer3.AllowedTags.Clear();        // No HTML tags allowed
                sanitizer3.AllowedAttributes.Clear();  // No attributes allowed either

                obj.Title = sanitizer3.Sanitize(obj.Title); // This removes <script>, <img>, etc

                // If after sanitization the title is empty, generate a safe fallback
                if (string.IsNullOrWhiteSpace(obj.Title))
                {
                    obj.Title = "Renamed Title " + GenerateRandomSuffix();
                }

                // ✅ Limit to 90 characters
                if (obj.Title.Length > 90)
                {
                    obj.Title = obj.Title.Substring(0, 90);
                }

                // ✅ Description
                obj.Description = obj.Description?.Trim() ?? "";
                if (obj.Description.Length > 490)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 490 characters.");
                }

                var sanitizer = new HtmlSanitizer();
                sanitizer.AllowedTags.Clear();

                sanitizer.AllowedTags.Add("b");
                sanitizer.AllowedTags.Add("i");
                sanitizer.AllowedTags.Add("u");
                // ✅ Make sure no hrefs sneak in
                sanitizer.AllowedAttributes.Clear();
                sanitizer.AllowedSchemes.Clear();

                sanitizer.KeepChildNodes = false;

                obj.Description = sanitizer.Sanitize(obj.Description);

                // ✅ Parse user-defined tags
                var userDefinedTags = await GetTagData(tagdata);

                // ✅ Ensure predefined tags exist
                if (predefinedTags == null || predefinedTags.Count < 2)
                {
                    ModelState.AddModelError("Tags", "Please select at least 2 predefined tags.");
                    return View(contentFromDb);
                }

                // ✅ Sanitize predefined tags
                var sanitizedPredefinedTags = predefinedTags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t =>
                    {
                        var tag = t.Trim();
                        tag = tag.Length > 30 ? tag.Substring(0, 30) : tag;
                        return System.Net.WebUtility.HtmlEncode(tag);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(name => new Tag { Name = name })
                    .ToList();

                // ✅ Combine & deduplicate
                var combinedTags = sanitizedPredefinedTags
                    .Union(userDefinedTags, new TagComparer())
                    .ToList();

                if (combinedTags.Count == 0)
                {
                    ModelState.AddModelError("Tags", "Please make sure there is at least one tag.");
                    return View(contentFromDb);
                }

                if (!ModelState.IsValid)
                {
                    return View(contentFromDb);
                }

                // ✅ Save updates
                contentFromDb.Title = obj.Title;
                contentFromDb.Description = obj.Description;
                contentFromDb.Tags = combinedTags;

                await _db.SaveChangesAsync();

                // ✅ Clear existing related content relations for this content
                var existingRelations = _db.UploadContentRelation
                    .Where(r => r.UploadContentId == contentFromDb.Id);
                _db.UploadContentRelation.RemoveRange(existingRelations);

                // ✅ Rebuild related content based on new tags
                var tagNames = combinedTags.Select(t => t.Name).ToList();

                var newRelated = await _db.UploadContents
                    .Include(uc => uc.Tags)
                    .Where(uc => uc.Id != contentFromDb.Id && uc.Tags.Any(tag => tagNames.Contains(tag.Name)))
                    .OrderByDescending(uc => uc.Tags.Count(tag => tagNames.Contains(tag.Name)))
                    .ThenByDescending(uc => uc.CreatedDateTime)
                    .Take(16)
                    .ToListAsync();

                var newRelations = newRelated.Select(r => new UploadContentRelation
                {
                    UploadContentId = contentFromDb.Id,
                    RelatedContentId = r.Id
                }).ToList();

                await _db.UploadContentRelation.AddRangeAsync(newRelations);
                await _db.SaveChangesAsync();
                return RedirectToAction("Display", "Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while editing post ID: {PostId}", obj.Id);

                TempData["EditError"] = "Something went wrong while saving your changes. Please try again.";

                return View(contentFromDb);
            }
        }

        public class TagComparer : IEqualityComparer<Tag>
        {
            public bool Equals(Tag x, Tag y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;

                return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Tag obj)
            {
                return obj?.Name?.ToLowerInvariant().GetHashCode() ?? 0;
            }
        }


        /// <summary>
        /// Display the delete page
        /// </summary>
        /// <param name="id">The id of the post to delete</param>
        /// <returns>a page</returns>
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var contentFromDb = await _db.UploadContents.FindAsync(id);

                if (contentFromDb == null)
                {
                    return NotFound();
                }

                return View(contentFromDb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading the delete view for content ID: {ContentId}", id);
                TempData["DeleteError"] = "Something went wrong while trying to load the delete page. Please try again later.";
                return RedirectToAction("Display", "Profile");
            }
        }

        /// <summary>
        /// Delete the post
        /// </summary>
        /// <param name="id">id of the post</param>
        /// <returns>a page</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePOST(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var contentFromDb = await _db.UploadContents.FindAsync(id);

                if (contentFromDb == null)
                {
                    return NotFound();
                }

                // Define paths
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                var mainFilePath = Path.Combine(_env.WebRootPath, contentFromDb.ContentPath.TrimStart('/'));
                var thumbnailFilePath = contentFromDb.ThumbnailPath != null
                    ? Path.Combine(_env.WebRootPath, contentFromDb.ThumbnailPath.TrimStart('/'))
                    : null;

                // Delete the main file (image/video)
                if (System.IO.File.Exists(mainFilePath))
                {
                    System.IO.File.Delete(mainFilePath);
                    _logger.LogInformation($"Deleted main file: {mainFilePath}");
                }
                else
                {
                    _logger.LogWarning($"Failed to delete main file (not found): {mainFilePath}");
                }

                // Delete the thumbnail file (if it exists)
                if (!string.IsNullOrEmpty(thumbnailFilePath) && System.IO.File.Exists(thumbnailFilePath))
                {
                    System.IO.File.Delete(thumbnailFilePath);
                    _logger.LogInformation($"Deleted thumbnail file: {thumbnailFilePath}");
                }
                else if (!string.IsNullOrEmpty(thumbnailFilePath))
                {
                    _logger.LogWarning($"Failed to delete thumbnail file (not found): {thumbnailFilePath}");
                }

                // First remove relations where this content is a RelatedContent
                var relatedAsMain = _db.UploadContentRelation
                    .Where(r => r.UploadContentId == contentFromDb.Id);
                var relatedAsRelated = _db.UploadContentRelation
                    .Where(r => r.RelatedContentId == contentFromDb.Id);

                _db.UploadContentRelation.RemoveRange(relatedAsMain);
                _db.UploadContentRelation.RemoveRange(relatedAsRelated);

                // Remove the content entry from the database
                _db.UploadContents.Remove(contentFromDb);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Content with ID: {id} was deleted successfully.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "We couldn't delete this content. Please contact support if the issue persists.";
                return RedirectToAction("Error", "Home");
            }

            return RedirectToAction("Display", "Profile");
        }


        /// <summary>
        /// redirects the user to the profile page
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult BackToList()
        {
            try
            {
                return RedirectToAction("Display", "Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redirection failed in BackToList.");
                return RedirectToAction("Error", "Home");
            }
        }

        private void ProcessImage(string inputPath, string outputPath, string thumbnailPath)
        {
            try
            {
                using (var originalImage = SixLabors.ImageSharp.Image.Load(inputPath))
                {
                    // 🔄 Auto-rotate based on EXIF orientation
                    originalImage.Mutate(x => x.AutoOrient());

                    var originalWidth = originalImage.Width;
                    var originalHeight = originalImage.Height;

                    // ✅ Generate Thumbnail (250x200)
                    var scaleFactorThumb = Math.Min(250f / originalWidth, 200f / originalHeight);
                    var thumbWidth = (int)(originalWidth * scaleFactorThumb);
                    var thumbHeight = (int)(originalHeight * scaleFactorThumb);

                    using (var thumbnail = originalImage.Clone(ctx =>
                        ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(thumbWidth, thumbHeight),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        })))
                    {
                        thumbnail.Save(thumbnailPath, new WebpEncoder { Quality = 100 });
                        _logger.LogInformation("Thumbnail saved at: {ThumbnailPath}", thumbnailPath);
                    }

                    // ✅ Generate Main Image (800x600 max)
                    var scaleFactorMain = Math.Min(800f / originalWidth, 600f / originalHeight);
                    var mainWidth = (int)(originalWidth * scaleFactorMain);
                    var mainHeight = (int)(originalHeight * scaleFactorMain);

                    using (var mainImage = originalImage.Clone(ctx =>
                        ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(mainWidth, mainHeight),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        })))
                    {
                        mainImage.Save(outputPath, new WebpEncoder { Quality = 95 });
                        _logger.LogInformation("Main image saved at: {MainPath}", outputPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image. InputPath: {InputPath}, OutputPath: {OutputPath}, ThumbPath: {ThumbnailPath}",
                    inputPath, outputPath, thumbnailPath);
                throw; // Rethrow to let calling method handle the exception gracefully
            }
        }



        private void ProcessGif(string inputPath, string outputPath, string thumbnailPath)
        {
            using (var collection = new MagickImageCollection())
            {
                //  Read GIF safely with FileStream to prevent file locks
                using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    collection.Read(fs);
                }

                collection.Coalesce(); //  Ensure proper frame merging to maintain transparency

                // ✅ Check if the GIF is larger than 800x600
                int originalWidth = (int)collection[0].Width;
                int originalHeight = (int)collection[0].Height;

                bool needsResize = originalWidth > 800 || originalHeight > 600; //  Only resize if it's too big
                
                //we're essentially checking if the image is smaller than the defined dimensions, if it is, then we don't need to resize
                if (needsResize)
                {
                    foreach (var frame in collection)
                    {
                        frame.FilterType = FilterType.Lanczos; // ✅ High-quality resizing
                        frame.Resize(new MagickGeometry(800, 600) { IgnoreAspectRatio = false });
                        frame.BackgroundColor = MagickColors.Transparent;
                    }
                }

                //  Write the resized GIF to MemoryStream
                byte[] resizedGif;
                using (var ms = new MemoryStream())
                {
                    collection.Write(ms, MagickFormat.Gif);
                    resizedGif = ms.ToArray();
                }

                //  Get the original file size and compare
                long originalSize = new FileInfo(inputPath).Length;
                long resizedSize = resizedGif.Length;

                if (resizedSize < originalSize)
                {
                    //  Use the resized GIF (smaller size)
                    System.IO.File.WriteAllBytes(outputPath, resizedGif);
                }
                else
                {
                    //  Keep the original GIF if it's smaller
                    System.IO.File.Copy(inputPath, outputPath, true);
                }

                //  Create an animated GIF thumbnail (250x200)
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

                    //  Preserve original animation properties
                    thumbnailCollection[0].AnimationIterations = 0; // Infinite loop

                    //  Save the animated GIF thumbnail
                    using (var msThumb = new MemoryStream())
                    {
                        thumbnailCollection.Write(msThumb, MagickFormat.Gif);
                        System.IO.File.WriteAllBytes(thumbnailPath, msThumb.ToArray());
                    }
                }
            }
        }



        private async Task ProcessVideo(IFormFile uploadedFile, string outputPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew(); // ⏱ Start timing

            var tempFolder = Path.Combine(_env.ContentRootPath, "Temp");
            Directory.CreateDirectory(tempFolder);
            string tempFilePath = Path.Combine(tempFolder, Path.GetRandomFileName() + ".mp4");
            string tempOutputPath = Path.Combine(tempFolder, Guid.NewGuid() + ".mp4");

            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }

                string ffmpegPath = Path.Combine(_env.WebRootPath, "tools");
                FFmpeg.SetExecutablesPath(ffmpegPath);
                string processedFilePath = outputPath.Replace(".mov", ".mp4");

                _logger.LogInformation("Processing video: {Input} -> {TempOutput}", tempFilePath, tempOutputPath);

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-i \"{tempFilePath}\" -movflags faststart -c:v copy -c:a copy \"{tempOutputPath}\"");

                await conversion.Start();

                if (!System.IO.File.Exists(tempOutputPath))
                {
                    throw new Exception($"FFmpeg conversion did not produce output: {tempOutputPath}");
                }

                System.IO.File.Move(tempOutputPath, processedFilePath);
                _logger.LogInformation("FFmpeg conversion successful, moved to: {OutputPath}", processedFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video upload. TempFile: {TempFile}, Output: {OutputPath}", tempFilePath, outputPath);
                throw;
            }
            finally
            {
                stopwatch.Stop(); // ⏱ Stop timer
                _logger.LogError("⏱ Video processing time: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

                try
                {
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                        _logger.LogDebug("Deleted temp input file: {TempPath}", tempFilePath);
                    }

                    if (System.IO.File.Exists(tempOutputPath))
                    {
                        System.IO.File.Delete(tempOutputPath);
                        _logger.LogDebug("Deleted temp output file: {TempPath}", tempOutputPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete temp video file: {TempPath}", tempFilePath);
                }
            }
        }


        private async Task ExtractVideoThumbnail(string videoPath, string thumbnailPath)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew(); // ⏱ Start timer

            string ffmpegPath = Path.Combine(_env.WebRootPath, "tools");
            FFmpeg.SetExecutablesPath(ffmpegPath);

            try
            {
                if (!System.IO.File.Exists(videoPath))
                {
                    throw new FileNotFoundException("Video file for thumbnail generation not found.", videoPath);
                }

                _logger.LogInformation("Extracting thumbnail from video: {VideoPath}", videoPath);

                var mediaInfo = await FFmpeg.GetMediaInfo(videoPath);
                double duration = mediaInfo.Duration.TotalSeconds;
                double timestamp = duration > 2 ? duration / 2 : 0.1;

                var conversion = await FFmpeg.Conversions.New()
                    .AddParameter($"-ss {timestamp} -i \"{videoPath}\" -vf \"scale=500:-1\" -frames:v 1 \"{thumbnailPath}\"")
                    .Start();

                if (!System.IO.File.Exists(thumbnailPath) || new FileInfo(thumbnailPath).Length < 1024)
                {
                    throw new IOException($"Thumbnail was not properly created or is too small: {thumbnailPath}");
                }

                try
                {
                    using var img = SixLabors.ImageSharp.Image.Load(thumbnailPath);
                }
                catch
                {
                    throw new IOException("Thumbnail file exists but could not be opened as a valid image.");
                }

                _logger.LogInformation("Thumbnail successfully created: {ThumbnailPath}", thumbnailPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract thumbnail for video: {VideoPath}", videoPath);
                throw;
            }
            finally
            {
                stopwatch.Stop(); // ⏱ Stop timer
                _logger.LogError("⏱ Thumbnail extraction time for {VideoPath}: {ElapsedMilliseconds} ms", videoPath, stopwatch.ElapsedMilliseconds);
            }
        }

    }
}
