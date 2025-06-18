using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BookKeepingWeb.Data;
using System.Security.Claims;

public class ChatController : Controller
{
    private readonly ApplicationDbContext _db;

    public ChatController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Display the chat page
    /// </summary>
    /// <returns></returns>
    public IActionResult Index()
    {
        string username = "Anonymous";

        if (User.Identity.IsAuthenticated)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userProfile = _db.UserProfiles.FirstOrDefault(up => up.UserId == userId);

            if (userProfile != null && !string.IsNullOrEmpty(userProfile.ScreenName))
            {
                username = userProfile.ScreenName;
            }
        }

        ViewData["Username"] = username; // Pass the username to the view
        return View();
    }
}
