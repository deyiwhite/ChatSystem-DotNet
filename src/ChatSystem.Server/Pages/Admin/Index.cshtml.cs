using ChatSystem.Core.Enums;
using ChatSystem.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatSystem.Server.Pages.Admin;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public int PendingUserCount { get; private set; }

    public int ActiveUserCount { get; private set; }

    public int MessageCount { get; private set; }

    public int DeletedMessageCount { get; private set; }

    public async Task OnGetAsync()
    {
        PendingUserCount = await _dbContext.Users.CountAsync(user =>
            user.Role == UserRole.User && user.Status == UserStatus.Pending);

        ActiveUserCount = await _dbContext.Users.CountAsync(user =>
            user.Role == UserRole.User && user.Status == UserStatus.Active);

        MessageCount = await _dbContext.Messages.CountAsync();
        DeletedMessageCount = await _dbContext.Messages.CountAsync(message => message.IsDeletedByAdmin);
    }
}
