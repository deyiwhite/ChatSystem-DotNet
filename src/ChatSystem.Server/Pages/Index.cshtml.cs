using System.Security.Claims;
using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(ApplicationDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public string DatabasePath { get; private set; } = string.Empty;

    public bool AdminExists { get; private set; }

    public int UserCount { get; private set; }

    public int FriendCount { get; private set; }

    public int PendingRequestCount { get; private set; }

    public int MessageCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Account/Login");
        }

        if (User.IsInRole("Admin"))
        {
            return RedirectToPage("/Admin/Index");
        }

        DatabasePath = ChatSystem.Server.Data.DatabasePath.GetDatabasePath(_environment.ContentRootPath);
        AdminExists = await _dbContext.Users.AnyAsync(user =>
            user.Username == "admin" &&
            user.Role == UserRole.Admin &&
            user.Status == UserStatus.Active);

        UserCount = await _dbContext.Users.CountAsync();

        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUserId = GetCurrentUserId();
            FriendCount = await _dbContext.Friends.CountAsync(friend => friend.UserId == currentUserId);
            PendingRequestCount = await _dbContext.FriendRequests.CountAsync(request =>
                request.ToUserId == currentUserId && request.Status == FriendRequestStatus.Pending);
            MessageCount = await _dbContext.Messages.CountAsync(message =>
                !message.IsDeletedByAdmin &&
                ((message.FromUserId == currentUserId && !message.IsDeletedBySender) ||
                 (message.ToUserId == currentUserId && !message.IsDeletedByReceiver)));
        }

        return Page();
    }

    private int GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) ? userId : 0;
    }
}
