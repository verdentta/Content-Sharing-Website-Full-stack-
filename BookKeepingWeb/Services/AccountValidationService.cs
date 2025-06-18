using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

public class AccountValidationService
{
    private readonly UserManager<IdentityUser> _userManager;

    public AccountValidationService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> IsUserStillValid(ClaimsPrincipal user)
    {
        var userId = _userManager.GetUserId(user);
        var userInDb = await _userManager.FindByIdAsync(userId);
        return userInDb != null;
    }
}
